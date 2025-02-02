using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace P2PNet
{
    /// <summary>
    /// Write/Read a network stream buffer for tcpt formatted data.
    /// </summary>
    public class Tcpt : IDisposable
    {
        static readonly byte[] tcpt = new byte[] { 116, 99, 112, 116 };
        byte[] _readBuffer = new byte[0];

        /// <summary>
        /// The buffer size after tcpt format is applied given the payload.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static int FinalBufferSize(int byteCount)
        {
            return byteCount + tcpt.Length + sizeof(uint);
        }

        /// <summary>
        /// Creates a tcpt formatted buffer.
        /// </summary>
        /// <param name="bytes">The payload data to be included in the packet.</param>
        /// <returns>A byte array containing the header, size, and payload.</returns>
        public byte[] CreateTcpt(byte[] bytes)
        {
            //buffer = header + bytes_size + bytes
            byte[] headerBuffer = tcpt;
            byte[] sizeBuffer = BitConverter.GetBytes((uint)bytes.Length);
            byte[] buffer = new byte[headerBuffer.Length + sizeBuffer.Length + bytes.Length];
            Array.Copy(headerBuffer, 0, buffer, 0, headerBuffer.Length);
            Array.Copy(sizeBuffer, 0, buffer, headerBuffer.Length, sizeBuffer.Length);
            Array.Copy(bytes, 0, buffer, headerBuffer.Length + sizeBuffer.Length, bytes.Length);

            return buffer;
        }

        /// <summary>
        /// Reads a tcpt format buffer.
        /// </summary>
        /// <returns>The extracted payload as a byte array, or an empty array if no complete packet is available.</returns>
        public byte[] ReadTcpt(byte[] buffer, int offset, int size)
        {
            // Expand readBuffer
            int readBufferInitLength = _readBuffer.Length;
            Array.Resize(ref _readBuffer, readBufferInitLength + size);
            Array.Copy(buffer, offset, _readBuffer, readBufferInitLength, size);

            return ReadTcpt();
        }

        byte[] ReadTcpt()
        {
            byte[] tempBuffer;

            //tcpt header
            int headerIndex = FindByteSequence(_readBuffer, 0, tcpt);

            if (headerIndex == -1)
            {
                //discard buffer if the header isnt found

                //last bytes could be part of the tag
                if (_readBuffer.Length >= tcpt.Length - 1)
                {
                    tempBuffer = new byte[tcpt.Length - 1]; //dirtyBuffer
                    Array.Copy(_readBuffer, _readBuffer.Length - (tcpt.Length - 1), tempBuffer, 0, tempBuffer.Length);
                    _readBuffer = tempBuffer;
                }

                return Array.Empty<byte>();
            }

            //discard all before header
            tempBuffer = new byte[_readBuffer.Length - headerIndex]; //cleanBuffer
            Array.Copy(_readBuffer, headerIndex, tempBuffer, 0, tempBuffer.Length);
            _readBuffer = tempBuffer;

            //header_size not available
            if (_readBuffer.Length - tcpt.Length < sizeof(uint))
            {
                return Array.Empty<byte>();
            }

            tempBuffer = new byte[sizeof(uint)]; //sizeBuffer
            Array.Copy(_readBuffer, tcpt.Length, tempBuffer, 0, tempBuffer.Length);
            uint bytesSize = BitConverter.ToUInt32(tempBuffer, 0);

            //second header index
            headerIndex = FindByteSequence(_readBuffer, tcpt.Length, tcpt);

            if (headerIndex != -1)
            {
                if (headerIndex - tcpt.Length - sizeof(uint) < bytesSize) //corrupted first header
                {
                    //discard corrupted first header
                    tempBuffer = new byte[_readBuffer.Length - headerIndex]; //dirtyBuffer
                    Array.Copy(_readBuffer, headerIndex, tempBuffer, 0, tempBuffer.Length);
                    _readBuffer = tempBuffer;

                    Console.WriteLine("_corrupted_tcpt_bytes_: " + (headerIndex-8));

                    return ReadTcpt(); //read second header
                }
            }

            //bytes not available
            if (_readBuffer.Length - tcpt.Length - sizeof(uint) < bytesSize)
            {
                return Array.Empty<byte>();
            }

            byte[] bytes = new byte[bytesSize];
            Array.Copy(_readBuffer, tcpt.Length + sizeof(uint), bytes, 0, bytes.Length);

            //discard header/header_size/bytes from original readBuffer
            tempBuffer = new byte[_readBuffer.Length - tcpt.Length - sizeof(uint) - bytesSize]; //dirtyBuffer
            Array.Copy(_readBuffer, tcpt.Length + sizeof(uint) + bytesSize, tempBuffer, 0, tempBuffer.Length);
            _readBuffer = tempBuffer;

            //return actual payload
            return bytes;
        }

        static int FindByteSequence(byte[] buffer, int startIndex, byte[] sequence)
        {
            if (sequence.Length > buffer.Length)
                return -1;

            bool match;

            for (int i = startIndex; i <= buffer.Length - sequence.Length; i++)
            {
                match = true;

                for (int j = 0; j < sequence.Length; j++)
                {
                    if (buffer[i + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        public void Dispose()
        {
            _readBuffer = null;
        }
    }
}
