using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[Serializable]
public class SITLCommsJsonIMUData {
    public float[] gyro = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] accel_body = new float[] { 0.0f, -9.8f, 0.0f };
}
[Serializable]
public class SITLCommsJsonOutputPacket {
    public double timestamp = 0;
    public SITLCommsJsonIMUData imu = new SITLCommsJsonIMUData();
    public float[] position = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] attitude = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] velocity = new float[] { 0.0f, 0.0f, 0.0f };
}
public class CopterJsonInterface : MonoBehaviour {
    public int localPort = 9002;
    
    private UdpClient socketReceive;
    private UdpClient socketSend;
    private Thread receiveThread;
    private Thread telemetryThread;
    private bool hasRemoteConnection;
    private IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);

    private SITLCommsJsonOutputPacket data = new SITLCommsJsonOutputPacket();
    private long startTime;

    [Header("Drone References")]
    public Rigidbody droneRb;
    public DroneController droneController;

    private const int MAGIC_16 = 18458;
    private const int MAGIC_32 = 29569;
    private const int PKT_LEN_16 = 2 + 2 + 4 + 16 * 2;
    private const int PKT_LEN_32 = 2 + 2 + 4 + 32 * 2;
    
    private Vector3 previousVelocity;
    public SITLCommsJsonOutputPacket Data => data;

    [Serializable]
    private struct SitlOutput {
        public ushort magic;
        public ushort frameRate;
        public uint frameCount;
        public ushort[] pwm;
    }

    void Start() {
        Debug.Log("Starting drone comms thread on port " + localPort);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        telemetryThread = new Thread(new ThreadStart(SendTelemetry));
        telemetryThread.IsBackground = true;
        telemetryThread.Start();

        // Init telemetry
        data.timestamp = 0.0f;
        data.imu = new SITLCommsJsonIMUData();
        data.imu.gyro = new float[] { 0.0f, 0.0f, 0.0f };
        data.imu.accel_body = new float[] { 0.0f, -9.8f, 0.0f };
        data.position = new float[] { 0.0f, 0.0f, 0.0f };
        data.attitude = new float[] { 0.0f, 0.0f, 0.0f };
        data.velocity = new float[] { 0.0f, 0.0f, 0.0f };
        
        startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        
        // Initialize previous velocity
        previousVelocity = droneRb.linearVelocity;
    }

    void Update() {
        data.timestamp = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime) / 1000.0;

        // Gyro: body-frame angular velocity (rad/s)
        // Unity to NED body frame transformation
        Vector3 gyroBody = transform.InverseTransformDirection(droneRb.angularVelocity);
        data.imu.gyro = new float[] { 
            gyroBody.z,   // Roll rate (around forward axis in NED body)
            gyroBody.x,   // Pitch rate (around right axis in NED body)
            -gyroBody.y   // Yaw rate (around down axis in NED body)
        };

        // Accel: specific force in body frame (m/s²)
        Vector3 dv = droneRb.linearVelocity - previousVelocity;
        Vector3 accelWorld = dv / Time.deltaTime;
        accelWorld -= Physics.gravity;
        Vector3 accelBody = transform.InverseTransformDirection(accelWorld);
        previousVelocity = droneRb.linearVelocity;
        
        data.imu.accel_body = new float[] { 
            accelBody.z,   // Forward (X in NED body)
            accelBody.x,   // Right (Y in NED body)
            -accelBody.y   // Down (Z in NED body)
        };

        // Position: NED earth frame (m)
        data.position = new float[] {
            droneRb.position.z,   // North
            droneRb.position.x,   // East
            -droneRb.position.y   // Down
        };

        // Attitude: roll, pitch, yaw (radians)
        Vector3 eulerDeg = droneRb.rotation.eulerAngles;
        float roll  = eulerDeg.z * Mathf.Deg2Rad;
        float pitch = eulerDeg.x * Mathf.Deg2Rad;
        float yaw   = -eulerDeg.y * Mathf.Deg2Rad;
        data.attitude = new float[] { roll, pitch, yaw };

        // Velocity: NED earth frame (m/s)
        data.velocity = new float[] {
            droneRb.linearVelocity.z,   // North
            droneRb.linearVelocity.x,   // East
            -droneRb.linearVelocity.y   // Down
        };
    }

    private void ReceiveData() {
        socketReceive = new UdpClient(localPort);
        socketSend = new UdpClient();

        while (true) {
            try {
                byte[] receivedData = socketReceive.Receive(ref remoteEndpoint);
                if (!hasRemoteConnection) {
                    hasRemoteConnection = true;
                    Debug.Log("Received new connection from SITL: " + remoteEndpoint.ToString());
                } else {
                    SitlOutput? pkt = ParseSitlPacket(receivedData);
                    if (pkt.HasValue) {
                        SitlOutput sitlPkt = pkt.Value;
                        droneController.UpdatePWM(sitlPkt.pwm);
                    }
                }
            } catch (Exception ex) {
                Debug.LogError($"Error receiving UDP data: {ex.Message}");
            }
        }
    }

    private void SendTelemetry() {
        while (true) {
            if (hasRemoteConnection && socketSend != null) {
                try {
                    string telemStr = JsonUtility.ToJson(data) + "\n";
                    byte[] byteData = Encoding.UTF8.GetBytes(telemStr);
                    socketSend.Send(byteData, byteData.Length, remoteEndpoint);
                } catch (Exception ex) {
                    Debug.LogError($"Error sending UDP data: {ex.Message}");
                }
            }
            
            // Small sleep to avoid spinning too fast
            Thread.Sleep(1);
        }
    }

    private SitlOutput? ParseSitlPacket(byte[] data) {
        if (data.Length != PKT_LEN_16 && data.Length != PKT_LEN_32) return null;

        using (var stream = new MemoryStream(data))
        using (var reader = new BinaryReader(stream, Encoding.UTF8, false)) {
            ushort magic = reader.ReadUInt16();
            ushort frameRate = reader.ReadUInt16();
            uint frameCount = reader.ReadUInt32();

            ushort[] pwm;
            if (magic == MAGIC_16 && data.Length == PKT_LEN_16) {
                pwm = new ushort[16];
                for (int i = 0; i < 16; i++) pwm[i] = reader.ReadUInt16();
            } else if (magic == MAGIC_32 && data.Length == PKT_LEN_32) {
                pwm = new ushort[32];
                for (int i = 0; i < 32; i++) pwm[i] = reader.ReadUInt16();
            } else {
                return null;
            }

            return new SitlOutput {
                magic = magic,
                frameRate = frameRate,
                frameCount = frameCount,
                pwm = pwm
            };
        }
    }

    void OnDisable() {
        if (receiveThread != null) {
            receiveThread.Abort();
        }
        if (telemetryThread != null) {
            telemetryThread.Abort();
        }
        if (socketReceive != null) {
            socketReceive.Close();
        }
        if (socketSend != null) {
            socketSend.Close();
        }
        Debug.Log("Stopping drone comms threads");
    }
}