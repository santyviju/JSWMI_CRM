namespace JSWMI_CRM.Models
{
    public class SupabaseConfig
    {
        public string Url { get; set; } = "https://your-project.supabase.co";
        public string Key { get; set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...";
        public string? ConnectionString { get; set; } = "Server=aws-0-ap-south-1.pooler.supabase.com;Port=6543;Database=postgres;User Id=postgres;Password=your_password;SSL Mode=Require;Trust Server Certificate=true";
        public bool UseCloudStorage { get; set; } = false; // When true, syncs directly with Supabase Cloud REST/Postgres
    }
}
