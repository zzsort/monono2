using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace monono2.AionMonoLib
{
    public class Camera
    {
        private Vector3 m_cameraPosition;
        private float m_yaw;
        private float m_pitch;

        public float StepSpeed = 16.0f;
        public float UpDownSpeed = 10.0f;

        public Camera(Vector3 initialPosition = new Vector3(), int initialYawDegrees = 135, int initialPitchDegrees = 20)
        {
            m_cameraPosition = initialPosition;
            m_yaw = MathHelper.ToRadians(initialYawDegrees);
            m_pitch = MathHelper.ToRadians(initialPitchDegrees);
        }

        public void ApplyToView(ref Matrix view)
        {
            var lookAtVector = new Vector3(0, -1, -m_pitch);
            
            var yawMatrix = Matrix.CreateRotationZ(m_yaw);
            lookAtVector = Vector3.Transform(lookAtVector, yawMatrix);
            lookAtVector += m_cameraPosition;

            var upVector = Vector3.UnitZ;
            view = Matrix.CreateLookAt(m_cameraPosition, lookAtVector, upVector);
        }

        public void Forward(float stepMultipler)
        {
            var forwardVector = new Vector3(0, StepSpeed * -stepMultipler, 0);

            var rotationMatrix = Matrix.CreateRotationZ(m_yaw);
            forwardVector = Vector3.Transform(forwardVector, rotationMatrix);

            m_cameraPosition += forwardVector;
        }

        public void Right(float stepMultipler)
        {
            var forwardVector = new Vector3(0, StepSpeed * -stepMultipler, 0);

            var rotationMatrix = Matrix.CreateRotationZ(m_yaw + MathHelper.ToRadians(90));
            forwardVector = Vector3.Transform(forwardVector, rotationMatrix);

            m_cameraPosition += forwardVector;
        }

        public void Turn(float rad)
        {
            m_yaw -= rad;

            while (m_yaw < 0) m_yaw += 2 * (float)Math.PI;
            while (m_yaw > (float)Math.PI) m_yaw -= 2 * (float)Math.PI;
        }

        public void Pitch(float rad)
        {
            m_pitch -= rad;

            while (m_pitch < 0) m_pitch += 2 * (float)Math.PI;
            while (m_pitch > (float)Math.PI) m_pitch -= 2 * (float)Math.PI;
        }

        public void MoveUp(float stepMultipler)
        {
            m_cameraPosition.Z += UpDownSpeed * stepMultipler;
        }

        public override string ToString()
        {
            float deg = MathHelper.ToDegrees(m_yaw);
            while (deg < 0) deg += 360;
            return $"Camera: {m_cameraPosition} Yaw: {deg} (H:{(int)(deg / 3)})";
        }

        public Vector3 GetCameraPosition()
        {
            return m_cameraPosition;
        }
    }
}
