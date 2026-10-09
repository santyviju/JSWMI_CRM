namespace JSWMI_CRM.Models
{
    public class SapB1Config
    {
        public string ServiceLayerUrl { get; set; } = "https://sapb1server.corp.jsw.in:50000/b1s/v1/";
        public string CompanyDB { get; set; } = "JSWMI_PROD";
        public string UserName { get; set; } = "manager";
        public string Password { get; set; } = "SAPB1Password#2026";
        public string TargetDocumentType { get; set; } = "ProductionOrder"; // ProductionOrder, CustomUDO
        public string UdoName { get; set; } = "JSW_CUTPLAN";
        public bool SimulationMode { get; set; } = true; // Set to true when live SAP B1 server is offline or in sandbox
        public bool BypassSslCertificate { get; set; } = true; // Common for internal self-signed SAP Service Layer SSL
    }
}
