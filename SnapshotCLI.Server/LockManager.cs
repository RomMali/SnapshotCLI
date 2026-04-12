using System.Collections.Concurrent;
using System.Text.Json;

namespace SnapshotCLI.Server
{
    public class LockManager
    {
        // Dictionary mapping: FilePath -> Username
        private ConcurrentDictionary<string, string> _locks;
        private readonly string _stateFilePath;

        public LockManager(string baseStorageDirectory)
        {
            // Save the state file right next to the Storage vault
            _stateFilePath = Path.Combine(baseStorageDirectory, "lock_state.json");
            _locks = LoadState();
        }

        // 1. Attempt to lock a file
        public bool TryLock(string targetPath, string username)
        {
            // TryAdd only succeeds if the key (targetPath) doesn't already exist
            if (_locks.TryAdd(targetPath, username))
            {
                SaveState();
                return true;
            }
            return false;
        }

        // 2. Attempt to unlock a file
        public bool TryUnlock(string targetPath, string username, bool isForceUnlock = false)
        {
            if (_locks.TryGetValue(targetPath, out string? owner))
            {
                // You can only unlock if you own it, OR if you are using a force override
                if (owner == username || isForceUnlock)
                {
                    if (_locks.TryRemove(targetPath, out _))
                    {
                        SaveState();
                        return true;
                    }
                }
            }
            return false; // File wasn't locked, or user doesn't own the lock
        }

        // 3. Check if an upload is allowed
        public bool CanModify(string targetPath, string username)
        {
            // If it's not locked, anyone can modify it.
            // If it IS locked, only the owner can modify it.
            if (_locks.TryGetValue(targetPath, out string? owner))
            {
                return owner == username;
            }
            return true; 
        }

        // 4. Find out who holds the lock (for error messages)
        public string GetLockOwner(string targetPath)
        {
            if (_locks.TryGetValue(targetPath, out string? owner))
            {
                return owner;
            }
            return "Nobody";
        }

        // --- Persistence Logic ---

        private void SaveState()
        {
            try
            {
                string json = JsonSerializer.Serialize(_locks);
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Could not save lock state: {ex.Message}");
            }
        }

        private ConcurrentDictionary<string, string> LoadState()
        {
            if (File.Exists(_stateFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_stateFilePath);
                    var loadedLocks = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(json);
                    if (loadedLocks != null) return loadedLocks;
                }
                catch
                {
                    Console.WriteLine("[Warning] Lock state file corrupted. Starting fresh.");
                }
            }
            return new ConcurrentDictionary<string, string>();
        }
    }
}