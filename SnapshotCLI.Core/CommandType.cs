namespace SnapshotCLI.Core;

// 1 byte is enough for 255 commands. Efficiency matters.
public enum CommandType : byte
{
    Handshake = 0x01,
    ListFiles = 0x02,
    DownloadFile = 0x03,
    UploadFile = 0x04,
    LockFile = 0x05,
    UnlockFile = 0x06,
    ForceUnlock = 0x07,
    Error = 0xFF
}
