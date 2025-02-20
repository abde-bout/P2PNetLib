using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PNet.Serialization
{
    public interface ISerializable
    {
        public byte[] Serialize(object obj);
        public object Deserialize(Serializer serializer);
    }
}
