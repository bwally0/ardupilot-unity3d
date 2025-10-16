using UnityEngine;

public class DroneController : MonoBehaviour {
    [Header("References")]
    public Rigidbody droneRb;

    [Header("Motor Settings")]
    public float minPWM = 1000f;
    public float maxPWM = 2000f;
    public float hoverPWM = 1500f; // Approx hover PWM (per motor)
    public float maxThrustPerMotor = 5f; // Newtons at maxPWM, tune to match drone mass
    public bool applyYawTorque = true;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private ushort[] pwmChannels = new ushort[4];
    private float lastTotalThrust = 0f;
    
    public ushort[] PWMChannels => pwmChannels;
    public float TotalThrust => lastTotalThrust;

    private void Start()
    {
        if (droneRb == null) droneRb = GetComponent<Rigidbody>();
    }

    public void UpdatePWM(ushort[] pwm) {
        if (pwm == null || pwm.Length < 4) return;
        for (int i = 0; i < 4; i++) {
            pwmChannels[i] = pwm[i];
        }
    }

    private void FixedUpdate() {
        if (pwmChannels == null || pwmChannels.Length < 4) return;

        float totalThrust = 0f;
        float yawTorque = 0f;

        for (int i = 0; i < 4; i++) {
            float norm = Mathf.InverseLerp(minPWM, maxPWM, pwmChannels[i]);
            float thrust = norm * maxThrustPerMotor;
            totalThrust += thrust;

            if (applyYawTorque) {
                int dir = (i % 2 == 0) ? 1 : -1;
                yawTorque += dir * thrust * 0.01f;
            }
        }

        lastTotalThrust = totalThrust; // store for UI

        Vector3 force = transform.up * totalThrust;
        droneRb.AddForce(force);

        if (applyYawTorque) {
            droneRb.AddRelativeTorque(Vector3.up * yawTorque);
        }
    }
}
