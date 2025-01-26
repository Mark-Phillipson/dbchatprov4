namespace DBChatPro
{
    public class AIConnection
    {
        public string ConnectionString { get; set;}="Data Source=Localhost;Initial Catalog=VoiceLauncher;Integrated Security=True;Connect Timeout=120;Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        public string Name { get; set; }="VoiceAdmin";
        public string DatabaseType { get; set; }="MSSQL";
    }
}
