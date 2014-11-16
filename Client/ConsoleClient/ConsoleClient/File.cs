using System.Xml.Serialization;

namespace Hermes
{
    public enum StatusType { Completed, Downloading, Paused, Canceled }

    [XmlType(TypeName="file")]
    public class HFile
    {
        [XmlAttribute(AttributeName="name")]
        public string Name;

        [XmlAttribute(AttributeName = "size")]
        public long Size;

        [XmlAttribute(AttributeName = "fileID")]
        public string ID;

        [XmlAttribute(AttributeName = "pieceSize")]
        public int PieceSize;

        [XmlAttribute(AttributeName = "blockSize")]
        public int BlockSize;

        [XmlAttribute(AttributeName = "status")]
        public StatusType Status;

        [XmlAttribute(AttributeName = "percentage")]
        public double Percentage;

        [XmlAttribute(AttributeName = "bitField")]
        public string BitField;

        [XmlElement("piece")]
        public Piece[] Pieces;
    }
}
