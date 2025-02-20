using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace P2PNet
{
    public readonly struct Code
    {
        static readonly Dictionary<int, Code> _codes = new();

        public int ID { get; }
        public ProtocolType Protocole { get; }

        Code(int id, ProtocolType protocole)
        {
            ID = id;
            Protocole = protocole;
        }

        public static Code Register(int id, ProtocolType protocole)
        {
            if (!_codes.ContainsKey(id))
            {
                Code code = new(id, protocole);
                _codes.Add(id, code);
                return code;
            }

            throw new InvalidOperationException($"Code with id {id} is already in use.");
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is Code code && ID.Equals(code.ID);
        }

        public override string ToString()
        {
            return $"ID: {ID}, Protocole: {Protocole}";
        }

        public static implicit operator int(Code code) => code.ID;
    }
}
