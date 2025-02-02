using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace P2PNet.Serialization
{
    public class Serializer : IDisposable
    {
        static readonly Dictionary<Type, Action<BinaryWriter, object>> Writers =
            new()
            {
                { typeof(int), (bw, value) => bw.Write((int)value) },
                { typeof(float), (bw, value) => bw.Write((float)value) },
                { typeof(double), (bw, value) => bw.Write((double)value) },
                { typeof(long), (bw, value) => bw.Write((long)value) },
                { typeof(short), (bw, value) => bw.Write((short)value) },
                { typeof(byte), (bw, value) => bw.Write((byte)value) },
                { typeof(bool), (bw, value) => bw.Write((bool)value) },
                { typeof(char), (bw, value) => bw.Write((char)value) },
                { typeof(string), (bw, value) => WriteString(bw, (string)value) }
            };

        static readonly Dictionary<Type, Func<BinaryReader, object>> Readers =
            new()
            {
                { typeof(int), br => br.ReadInt32() },
                { typeof(float), br => br.ReadSingle() },
                { typeof(double), br => br.ReadDouble() },
                { typeof(long), br => br.ReadInt64() },
                { typeof(short), br => br.ReadInt16() },
                { typeof(byte), br => br.ReadByte() },
                { typeof(bool), br => br.ReadBoolean() },
                { typeof(char), br => br.ReadChar() },
               { typeof(string), ReadString }
            };

        public byte[] Bytes => _memoryStream.ToArray();

        MemoryStream _memoryStream;
        BinaryReader _binaryReader;
        BinaryWriter _binaryWriter;

        /// <summary>
        /// Serialize constructor
        /// </summary>
        public Serializer()
        {
            _memoryStream = new MemoryStream();
            _binaryWriter = new BinaryWriter(_memoryStream);
        }

        /// <summary>
        /// Deserialize constructor
        /// </summary>
        /// <param name="bytes"></param>
        public Serializer(byte[] bytes)
        {
            _memoryStream = new(bytes);
            _binaryReader = new(_memoryStream);
        }

        public static T DeserializeItem<T>(byte[] bytes)
        {
            return (T)DeserializeItem(bytes, typeof(T));
        }
        public static object DeserializeItem(byte[] bytes, Type type)
        {
            using Serializer serializer = new(bytes);
            return serializer.GetNextItem(type);
        }

        public T GetNextItem<T>() => (T)GetNextItem(typeof(T));
        public object GetNextItem(Type type)
        {
            if (Readers.TryGetValue(type, out var readFunc))
            {
                return readFunc(_binaryReader);
            }
            else if (typeof(ISerializable).IsAssignableFrom(type))
            {
                var instance = Activator.CreateInstance(type) as ISerializable;
                return instance.Deserialize(this);
            }
            else if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                var length = GetNextItem<int>();
                Array array = Array.CreateInstance(elementType, length);

                for (int i = 0; i < length; i++)
                {
                    var item = GetNextItem(elementType);
                    array.SetValue(Convert.ChangeType(item, elementType), i);
                }

                return array;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported type: {type}");
            }
        }

        /// <summary>
        /// Serializes primitive data types, arrays of primitive data types, 
        /// and objects implementing the <see cref="ISerializable"/> interface.
        /// </summary>
        /// <param name="items">The items to be serialized.</param>
        /// <returns>A byte array containing the serialized data of the provided items.</returns>
        public static byte[] SerializeItems(params object[] items)
        {
            using Serializer serializer = new();

            foreach (var item in items)
            {
                serializer.SerializeItem(item);
            }

            return serializer._memoryStream.ToArray();
        }

        public void SerializeItem(object item)
        {
            var type = item.GetType();

            if (Writers.TryGetValue(type, out var writeAction))
            {
                writeAction(_binaryWriter, item);
            }
            else if (item is ISerializable serializable)
            {
                _binaryWriter.Write(serializable.Serialize(item));
            }
            else if (type.IsArray)
            {
                SerializeItem(((ICollection)item).Count);

                var enumrable = (IEnumerable)item;
                foreach (var element in enumrable)
                {
                    SerializeItem(element);
                }
            }
            else
            {
                throw new InvalidOperationException($"Unsupported type: {type}");
            }
        }

        // Custom methods for handling string serialization/deserialization
        static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value.Length);
            writer.Write(Encoding.UTF8.GetBytes(value));
        }

        static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        public void Dispose()
        {
            _memoryStream?.Dispose();
            _binaryReader?.Dispose();
        }
    }
}
