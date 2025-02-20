using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace P2PNet
{
    /// <summary>
    /// Reads tcpt formatted packets from a buffer.
    /// </summary>
    public class TcptReader : IDisposable
    {
        private const int SIZE_FIELD_LENGTH = sizeof(uint);
        public static readonly byte[] tcptHeader = new byte[] { 116, 99, 112, 116 };

        private byte[] _buffer = new byte[4096];
        private int _bufferStart;
        private int _bufferCount;

        public TcptReader()
        {
        }

        /// <summary>
        /// Reads tcpt formatted packets.
        /// </summary>
        /// <returns>The extracted payloads as a list of byte arrays, or an empty list if no complete packet is available.</returns>
        public List<byte[]> ReadTcptPackets(byte[] inputBuffer, int offset, int size)
        {
            AppendToBuffer(inputBuffer, offset, size);
            List<byte[]> packets = new List<byte[]>();

            while (TryReadNextPacket(out byte[] packet))
            {
                packets.Add(packet);
            }

            return packets;
        }

        private void AppendToBuffer(byte[] data, int sourceOffset, int bytesToCopy)
        {
            EnsureBufferCapacity(bytesToCopy);
            Array.Copy(data, sourceOffset, _buffer, _bufferStart + _bufferCount, bytesToCopy);
            _bufferCount += bytesToCopy;
        }

        private void EnsureBufferCapacity(int requiredSpace)
        {
            int availableSpace = _buffer.Length - (_bufferStart + _bufferCount);
            if (availableSpace >= requiredSpace) return;

            int newCapacity = Math.Max(_buffer.Length * 2, _bufferCount + requiredSpace);
            byte[] newBuffer = new byte[newCapacity];
            Array.Copy(_buffer, _bufferStart, newBuffer, 0, _bufferCount);
            _buffer = newBuffer;
            _bufferStart = 0;
        }

        private bool TryReadNextPacket(out byte[] packet)
        {
            packet = Array.Empty<byte>();
            if (!TryFindHeader(out int headerPosition)) return false;

            AdvanceBuffer(headerPosition);
            if (!TryReadPayloadSize(out uint payloadSize)) return false;
            if (!HasCompletePayload(payloadSize, out int totalPacketLength)) return false;

            if (CheckForNestedHeader(payloadSize, totalPacketLength))
            {
                return TryReadNextPacket(out packet);
            }

            packet = ExtractPayload(totalPacketLength, payloadSize);
            AdvanceBuffer(totalPacketLength);
            return true;
        }

        private bool TryFindHeader(out int headerPosition)
        {
            headerPosition = -1;
            if (_bufferCount < tcptHeader.Length) return false;

            ReadOnlySpan<byte> bufferSpan = new ReadOnlySpan<byte>(_buffer, _bufferStart, _bufferCount);
            headerPosition = FindSequence(bufferSpan, new ReadOnlySpan<byte>(tcptHeader));
            return headerPosition != -1;
        }

        private bool TryReadPayloadSize(out uint payloadSize)
        {
            payloadSize = 0;
            if (_bufferCount < tcptHeader.Length + SIZE_FIELD_LENGTH) return false;

            payloadSize = BitConverter.ToUInt32(_buffer, _bufferStart + tcptHeader.Length);
            return true;
        }

        private bool HasCompletePayload(uint payloadSize, out int totalPacketLength)
        {
            totalPacketLength = tcptHeader.Length + SIZE_FIELD_LENGTH + (int)payloadSize;
            return _bufferCount >= totalPacketLength;
        }

        private bool CheckForNestedHeader(uint payloadSize, int totalPacketLength)
        {
            ReadOnlySpan<byte> payloadSpan = new ReadOnlySpan<byte>(
                _buffer,
                _bufferStart + tcptHeader.Length + SIZE_FIELD_LENGTH,
                (int)payloadSize
            );

            int nestedHeaderPosition = FindSequence(payloadSpan, new ReadOnlySpan<byte>(tcptHeader));
            if (nestedHeaderPosition == -1) return false;

            int corruptedBytes = tcptHeader.Length + SIZE_FIELD_LENGTH + nestedHeaderPosition;
            AdvanceBuffer(corruptedBytes);
            Console.WriteLine($"Detected corrupted packet, skipped {corruptedBytes} bytes");
            return true;
        }

        private byte[] ExtractPayload(int totalPacketLength, uint payloadSize)
        {
            byte[] payload = new byte[payloadSize];
            Array.Copy(
                _buffer,
                _bufferStart + tcptHeader.Length + SIZE_FIELD_LENGTH,
                payload,
                0,
                payloadSize
            );
            return payload;
        }

        private void AdvanceBuffer(int bytesToAdvance)
        {
            _bufferStart += bytesToAdvance;
            _bufferCount -= bytesToAdvance;

            if (_bufferCount == 0)
            {
                _bufferStart = 0;
            }
        }

        private int FindSequence(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> sequence)
        {
            for (int i = 0; i <= buffer.Length - sequence.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (buffer[i + j] != sequence[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        /// <summary>
        /// The buffer size after tcpt format is applied given the payload.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static int FinalBufferSize(int byteCount)
        {
            return byteCount + tcptHeader.Length + sizeof(uint);
        }
       
        public void Dispose()
        {
            _buffer = null;
        }
    }
}
