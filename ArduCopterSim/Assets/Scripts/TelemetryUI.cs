using UnityEngine;
using TMPro;

public class TelemetryUI : MonoBehaviour {
    [Header("References")]
    public CopterJsonInterface copter;
    public DroneController controller;
    public TextMeshProUGUI telemetryText;

    void Update() {
        if (copter == null || telemetryText == null) return;

        var d = copter.Data;

        // Build PWM string per motor
        string pwmStr = "[N/A]";
        if (controller != null && controller.PWMChannels != null && controller.PWMChannels.Length >= 4) {
            pwmStr =
                $"M1: {controller.PWMChannels[0]}  " +
                $"M2: {controller.PWMChannels[1]}  " +
                $"M3: {controller.PWMChannels[2]}  " +
                $"M4: {controller.PWMChannels[3]}";
        }

        telemetryText.text =
            $"--- Telemetry ---\n" +
            $"Timestamp: {d.timestamp:F2}\n" +
            $"Gyro: [{d.imu.gyro[0]:F2}, {d.imu.gyro[1]:F2}, {d.imu.gyro[2]:F2}]\n" +
            $"Accel: [{d.imu.accel_body[0]:F2}, {d.imu.accel_body[1]:F2}, {d.imu.accel_body[2]:F2}]\n" +
            $"Pos (NED): [{d.position[0]:F2}, {d.position[1]:F2}, {d.position[2]:F2}]\n" +
            $"Vel (NED): [{d.velocity[0]:F2}, {d.velocity[1]:F2}, {d.velocity[2]:F2}]\n" +
            $"Att (Euler): [{d.attitude[0]:F2}, {d.attitude[1]:F2}, {d.attitude[2]:F2}]\n\n" +

            $"--- Controller ---\n" +
            $"PWM per Motor: {pwmStr}\n" +
            $"Total Thrust: {(controller != null ? controller.TotalThrust.ToString("F2") : "N/A")} N\n" +
            $"Hover PWM: {(controller != null ? controller.hoverPWM.ToString("F0") : "N/A")}\n" +
            $"Mass: {(controller != null ? controller.droneRb.mass.ToString("F2") : "N/A")} kg";
    }
}
