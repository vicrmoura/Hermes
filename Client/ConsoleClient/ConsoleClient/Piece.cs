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

        [XmlAttribute(AttributeName = "size")]
        public int Size;
    }
}
