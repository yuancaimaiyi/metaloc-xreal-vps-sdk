using UnityEngine;

namespace Metaloc.VPS
{
    /// <summary>
    /// Coordinate system helpers used throughout the SDK.
    /// Unity uses left-hand; VPS server uses right-hand.
    /// </summary>
    internal static class MetalocCoordUtils
    {
        public static Pose LeftHandToRightHand(Pose lp)
        {
            return new Pose(
                new Vector3(lp.position.x, -lp.position.y, lp.position.z),
                new Quaternion(lp.rotation.x, -lp.rotation.y, lp.rotation.z, -lp.rotation.w));
        }

        public static Pose RightHandToLeftHand(Pose rp)
        {
            return new Pose(
                new Vector3(rp.position.x, -rp.position.y, rp.position.z),
                new Quaternion(rp.rotation.x, -rp.rotation.y, rp.rotation.z, -rp.rotation.w));
        }

        public static Pose MultiplyPose(Pose a, Pose b)
        {
            return new Pose(a.position + a.rotation * b.position, a.rotation * b.rotation);
        }

        public static Pose InversePose(Pose p)
        {
            Quaternion invR = Quaternion.Inverse(p.rotation);
            return new Pose(invR * -p.position, invR);
        }

        /// <summary>Mirror raw R8 grayscale data horizontally (needed for XREAL gray camera output).</summary>
        public static byte[] MirrorHorizontal(byte[] data, int width, int height)
        {
            byte[] mirrored = new byte[data.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    mirrored[row + (width - 1 - x)] = data[row + x];
            }
            return mirrored;
        }
    }
}
