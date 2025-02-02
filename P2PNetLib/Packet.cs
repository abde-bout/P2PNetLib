using P2PNet.Serialization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace P2PNet
{
    public readonly struct Packet : ISerializable
    {
        public int Code { get; }
        public int ID { get; }
        public byte[] Bytes { get; }

        public Packet(int code, int id, byte[] bytes)
        {
            Code = code;
            Bytes = bytes;
            ID = id;
        }

        public override string ToString()
        {
            return $"Code: {Code}\n" +
                $"ID: {ID}\n" +
                $"bytes_count: {Bytes.Length}\n";
        }

        public byte[] Serialize(object obj)
        {
            return Serializer.SerializeItems(((Packet)obj).Code,
                ((Packet)obj).ID,
                ((Packet)obj).Bytes);
        }

        public object Deserialize(Serializer serializer)
        {
            return new Packet(serializer.GetNextItem<int>(),
                serializer.GetNextItem<int>(),
                serializer.GetNextItem<byte[]>());
        }
    }
}
