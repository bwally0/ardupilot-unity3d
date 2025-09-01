using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[Serializable]
public class SITLCommsJsonIMUData
{
    public float[] gyro = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] accel_body = new float[] { 0.0f, -9.8f, 0.0f };
}

[Serializable]
public class SITLCommsJsonOutputPacket
{
    public float timestamp = 0;
    public SITLCommsJsonIMUData imu = new SITLCommsJsonIMUData();
    public float[] position = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] attitude = new float[] { 0.0f, 0.0f, 0.0f };
    public float[] velocity = new float[] { 0.0f, 0.0f, 0.0f };
}

public class CopterJsonInterface : MonoBehaviour
{
    public int localPort = 9002;
    private UdpClient socketReceive;
    private UdpClient socketSend;
    private Thread receiveThread;
    private Thread sendThread;
    private bool isRunning;
    private bool hasRemoteConnection;
    private IPEndPoint remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
    private SITLCommsJsonOutputPacket data = new SITLCommsJsonOutputPacket();
    private long startTime;
    private const int MAGIC_16 = 18458; // 16 channel output
    private const int MAGIC_32 = 29569; // 32 channel output
    private const int PKT_LEN_16 = 2 + 2 + 4 + 16 * 2; // 40 bytes
    private const int PKT_LEN_32 = 2 + 2 + 4 + 32 * 2; // 72 bytes

    [Serializable]
    private struct SitlOutput
    {
        public ushort magic;
        public ushort frameRate;
        public uint frameCount;
        public ushort[] pwm;
    }

    void Start()
    {
        try
        {
            socketReceive = new UdpClient(localPort);
            socketSend = new UdpClient();
            isRunning = true;

            // Initialize data
            data.timestamp = 0.0f;
            data.imu = new SITLCommsJsonIMUData();
            data.imu.gyro = new float[] { 0.0f, 0.0f, 0.0f };
            data.imu.accel_body = new float[] { 0.0f, -9.8f, 0.0f };
            data.position = new float[] { 0.0f, 0.0f, 0.0f };
            data.attitude = new float[] { 0.0f, 0.0f, 0.0f };
            data.velocity = new float[] { 0.0f, 0.0f, 0.0f };

            startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Start receiving thread
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // Start sending thread
            sendThread = new Thread(new ThreadStart(SendData));
            sendThread.IsBackground = true;
            sendThread.Start();

            Debug.Log($"Starting UDP comms on port {localPort}...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error initializing UDP client: {ex.Message}");
        }
    }

    private void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                byte[] receivedData = socketReceive.Receive(ref remoteEndpoint);
                if (!hasRemoteConnection)
                {
                    hasRemoteConnection = true;
                    Debug.Log($"Received new connection from SITL: {remoteEndpoint}");
                }

                SitlOutput? pkt = ParseSitlPacket(receivedData);
                if (pkt.HasValue)
                {
                    SitlOutput sitlPkt = pkt.Value;
                    int channels = sitlPkt.pwm.Length;
                    string pwmStr = string.Join(", ", sitlPkt.pwm);
                    Debug.Log($"SITL RX: magic={sitlPkt.magic} frame_rate={sitlPkt.frameRate} frame_count={sitlPkt.frameCount} channels={channels} pwm=[{pwmStr}]");
                }
                else
                {
                    Debug.Log($"Ignoring invalid packet len={receivedData.Length} from {remoteEndpoint} hex={BitConverter.ToString(receivedData)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error receiving UDP data: {ex.Message}");
            }
        }
    }

    private SitlOutput? ParseSitlPacket(byte[] data)
    {
        if (data.Length != PKT_LEN_16 && data.Length != PKT_LEN_32)
        {
            return null;
        }

        using (var stream = new MemoryStream(data))
        using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
        {
            ushort magic = reader.ReadUInt16();
            ushort frameRate = reader.ReadUInt16();
            uint frameCount = reader.ReadUInt32();

            ushort[] pwm;
            if (magic == MAGIC_16 && data.Length == PKT_LEN_16)
            {
                pwm = new ushort[16];
                for (int i = 0; i < 16; i++)
                {
                    pwm[i] = reader.ReadUInt16();
                }
            }
            else if (magic == MAGIC_32 && data.Length == PKT_LEN_32)
            {
                pwm = new ushort[32];
                for (int i = 0; i < 32; i++)
                {
                    pwm[i] = reader.ReadUInt16();
                }
            }
            else
            {
                return null;
            }

            return new SitlOutput
            {
                magic = magic,
                frameRate = frameRate,
                frameCount = frameCount,
                pwm = pwm
            };
        }
    }

    private void SendData()
    {
        while (isRunning)
        {
            try
            {
                if (hasRemoteConnection)
                {
                    // Update timestamp
                    data.timestamp = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime) / 1000.0f;

                    // Serialize data to JSON
                    string telemStr = JsonUtility.ToJson(data) + "\n";
                    byte[] byteData = Encoding.UTF8.GetBytes(telemStr);

                    // Send UDP packet
                    socketSend.Send(byteData, byteData.Length, remoteEndpoint);
                    Debug.Log($"Sent frame to {remoteEndpoint}: {telemStr.Trim()}");
                }

            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending UDP data: {ex.Message}");
            }
        }
    }

    void OnDisable()
    {
        isRunning = false;
        if (receiveThread != null)
        {
            receiveThread.Abort();
        }
        if (sendThread != null)
        {
            sendThread.Abort();
        }
        if (socketReceive != null)
        {
            socketReceive.Close();
        }
        if (socketSend != null)
        {
            socketSend.Close();
        }
        Debug.Log("UDP Comms stopped");
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null)
        {
            receiveThread.Abort();
        }
        if (sendThread != null)
        {
            sendThread.Abort();
        }
        if (socketReceive != null)
        {
            socketReceive.Close();
        }
        if (socketSend != null)
        {
            socketSend.Close();
        }
        Debug.Log("UDP Comms stopped");
    }
}