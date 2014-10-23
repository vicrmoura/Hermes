using System.Xml.Serialization;

namespace Hermes
{
    [XmlType(TypeName = "piece")]
    public class Piece
    {
        [XmlAttribute(AttributeName = "sha")]
        public string Sha;

        [XmlAttribute(AttributeName = "bitField")]
        public string BitField;

        private uint size;
        [XmlAttribute(AttributeName = "size")]
        public uint Size
        {
            get { return size; }
            set { size = value; SizeSpecified = true; }
        }
        [XmlIgnore]
        public bool SizeSpecified;
    }
}
