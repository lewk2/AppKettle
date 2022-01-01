namespace AppKettle
{
    public interface IAppKettleHexMessage
    {
        public byte HeaderByte { get; set; }

        public ushort Length { get; set; }

        public byte Sequence { get; set; }

        public KettleCmd Command { get; set; }

        public byte[] Data { get; set; }

        public byte Checksum { get; set; }
    }
}