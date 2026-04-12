using System.Text;
using System.Text.Json;

namespace SnapshotCLI.Core
{
    public class NetworkPacket
    {
        public CommandType Command { get; set; }
        
        // --- ENTERPRISE IDENTITY ---
        public string MachineId { get; set; } = string.Empty;   // The true lock owner
        public string DisplayName { get; set; } = "Anonymous";  // For human-readable logs
        
        public string Project { get; set; } = "SnapshotCLI"; 
        public string Branch { get; set; } = "Main";         
        
        public string? Payload { get; set; }

        public byte[] ToBytes()
        {
            string json = JsonSerializer.Serialize(this);
            return Encoding.UTF8.GetBytes(json);
        }

        public static NetworkPacket? FromBytes(byte[] bytes)
        {
            try 
            {
                string json = Encoding.UTF8.GetString(bytes);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<NetworkPacket>(json);
            }
            catch 
            {
                return null;
            }
        }
    }
}