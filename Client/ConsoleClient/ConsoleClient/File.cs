using System.Xml.Serialization;

namespace Hermes
{
    public enum StatusType { Completed, Downloading, Started, Paused, Canceled }

    [XmlType(TypeName="file")]
    public class File
    {
        [XmlAttribute(AttributeName="name")]
        public string Name;

        [XmlAttribute(AttributeName = "size")]
        public uint Size;

        [XmlAttribute(AttributeName = "fileID")]
        public string ID;

        [XmlAttribute(AttributeName = "pieceSize")]
        public uint PieceSize;

        [XmlAttribute(AttributeName = "blockSize")]
        public uint BlockSize;

        [XmlAttribute(AttributeName = "status")]
        public StatusType Status;

        private double percentage;
        [XmlAttribute(AttributeName = "percentage")]
        public double Percentage
        {
            get { return percentage; }
            set { percentage = value; PercentageSpecified = true; }
        }
        [XmlIgnore]
        public bool PercentageSpecified;

        [XmlAttribute(AttributeName = "bitField")]
        public string BitField;

        [XmlElement("piece")]
        public Piece[] Pieces;
    }
}
