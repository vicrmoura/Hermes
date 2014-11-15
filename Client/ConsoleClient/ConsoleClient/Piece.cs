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

        private int size;
        [XmlAttribute(AttributeName = "size")]
        public int Size
        {
            get { return size; }
            set { size = value; SizeSpecified = true; }
        }
        [XmlIgnore]
        public bool SizeSpecified;
    }
}
