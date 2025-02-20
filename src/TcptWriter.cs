using System;

namespace P2PNet
{
    public class TcptWriter
    {
        /// <summary>
        /// Creates a tcpt formatted buffer.
        /// </summary>
        /// <param name="bytes">The payload data to be included in the packet.</param>
        /// <returns></returns>
        public static byte[] CreateTcptPacket(byte[] bytes)
        {
            //buffer = header + bytes_size + bytes
            byte[] headerBuffer = TcptReader.tcptHeader;
            byte[] sizeBuffer = BitConverter.GetBytes((uint)bytes.Length);
            byte[] buffer = new byte[headerBuffer.Length + sizeBuffer.Length + bytes.Length];
            Array.Copy(headerBuffer, 0, buffer, 0, headerBuffer.Length);
            Array.Copy(sizeBuffer, 0, buffer, headerBuffer.Length, sizeBuffer.Length);
            Array.Copy(bytes, 0, buffer, headerBuffer.Length + sizeBuffer.Length, bytes.Length);

            return buffer;
        }
    }
}