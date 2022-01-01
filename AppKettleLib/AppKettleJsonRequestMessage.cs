namespace AppKettle
{
    public class AppKettleJsonRequestMessage
    {
        public string app_cmd { get; set; } = "62";
        public string imei { get; set; }
        public string SubDev { get; set; } = string.Empty;
        public string data2 { get; set; }
    }
}