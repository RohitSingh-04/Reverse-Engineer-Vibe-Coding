using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AuraWallpaper.Core.Models;
using Microsoft.Data.Sqlite;

namespace AuraWallpaper.Core.Storage
{
    public class AuraDatabase : IDisposable
    {
        private static AuraDatabase? _instance;
        private static readonly object _lock = new object();
        public static AuraDatabase Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= new AuraDatabase();
                }
            }
        }

        private readonly string _dbPath;
        private readonly string _connectionString;

        public string DatabasePath => _dbPath;

        public AuraDatabase(string? customDbPath = null)
        {
            if (string.IsNullOrEmpty(customDbPath))
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraWallpaper");
                Directory.CreateDirectory(appData);
                _dbPath = Path.Combine(appData, "aura_wallpaper.db");
            }
            else
            {
                _dbPath = customDbPath;
                string? dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            InitializeDatabase();
        }

        private SqliteConnection CreateConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private void InitializeDatabase()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS wallpapers (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    description TEXT,
                    author TEXT,
                    version TEXT,
                    contact TEXT,
                    license TEXT,
                    type INTEGER NOT NULL,
                    filepath TEXT NOT NULL,
                    previewpath TEXT,
                    thumbnailpath TEXT,
                    tags TEXT,
                    category TEXT,
                    isfavorite INTEGER NOT NULL DEFAULT 0,
                    dateadded TEXT NOT NULL,
                    lastused TEXT NOT NULL,
                    sourceurl TEXT,
                    arguments TEXT,
                    customproperties TEXT,
                    propertyvalues TEXT,
                    livelymetadata TEXT
                );

                CREATE TABLE IF NOT EXISTS monitor_assignments (
                    monitor_device_id TEXT PRIMARY KEY,
                    monitor_index INTEGER NOT NULL,
                    wallpaper_id TEXT NOT NULL,
                    mode INTEGER NOT NULL DEFAULT 0,
                    state INTEGER NOT NULL DEFAULT 0,
                    volume INTEGER NOT NULL DEFAULT 80,
                    is_muted INTEGER NOT NULL DEFAULT 0,
                    speed REAL NOT NULL DEFAULT 1.0,
                    scaling INTEGER NOT NULL DEFAULT 0,
                    assigned_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS playlists (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    rotation_interval INTEGER NOT NULL DEFAULT 300,
                    is_shuffle INTEGER NOT NULL DEFAULT 0,
                    is_repeat INTEGER NOT NULL DEFAULT 1,
                    is_active INTEGER NOT NULL DEFAULT 0,
                    target_monitor INTEGER NOT NULL DEFAULT -1,
                    created_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS playlist_items (
                    id TEXT PRIMARY KEY,
                    playlist_id TEXT NOT NULL,
                    wallpaper_id TEXT NOT NULL,
                    order_index INTEGER NOT NULL DEFAULT 0,
                    custom_duration INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(playlist_id) REFERENCES playlists(id) ON DELETE CASCADE
                );

                CREATE TABLE IF NOT EXISTS application_rules (
                    id TEXT PRIMARY KEY,
                    process_name TEXT NOT NULL UNIQUE,
                    action INTEGER NOT NULL DEFAULT 0,
                    description TEXT,
                    is_enabled INTEGER NOT NULL DEFAULT 1
                );

                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    wallpaper_id TEXT NOT NULL,
                    applied_at TEXT NOT NULL,
                    monitor_id TEXT
                );
            ";
            cmd.ExecuteNonQuery();
        }

        #region Wallpapers CRUD

        public void SaveWallpaper(WallpaperItem item)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT OR REPLACE INTO wallpapers (
                    id, title, description, author, version, contact, license,
                    type, filepath, previewpath, thumbnailpath, tags, category,
                    isfavorite, dateadded, lastused, sourceurl, arguments,
                    customproperties, propertyvalues, livelymetadata
                ) VALUES (
                    @id, @title, @description, @author, @version, @contact, @license,
                    @type, @filepath, @previewpath, @thumbnailpath, @tags, @category,
                    @isfavorite, @dateadded, @lastused, @sourceurl, @arguments,
                    @customproperties, @propertyvalues, @livelymetadata
                );
            ";

            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@title", item.Title);
            cmd.Parameters.AddWithValue("@description", item.Description);
            cmd.Parameters.AddWithValue("@author", item.Author);
            cmd.Parameters.AddWithValue("@version", item.Version);
            cmd.Parameters.AddWithValue("@contact", item.Contact);
            cmd.Parameters.AddWithValue("@license", item.License);
            cmd.Parameters.AddWithValue("@type", (int)item.Type);
            cmd.Parameters.AddWithValue("@filepath", item.FilePath);
            cmd.Parameters.AddWithValue("@previewpath", item.PreviewPath);
            cmd.Parameters.AddWithValue("@thumbnailpath", item.ThumbnailPath);
            cmd.Parameters.AddWithValue("@tags", item.Tags);
            cmd.Parameters.AddWithValue("@category", item.Category);
            cmd.Parameters.AddWithValue("@isfavorite", item.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("@dateadded", item.DateAdded.ToString("O"));
            cmd.Parameters.AddWithValue("@lastused", item.LastUsed.ToString("O"));
            cmd.Parameters.AddWithValue("@sourceurl", item.SourceUrl);
            cmd.Parameters.AddWithValue("@arguments", item.Arguments);
            cmd.Parameters.AddWithValue("@customproperties", item.CustomPropertiesJson);
            cmd.Parameters.AddWithValue("@propertyvalues", item.PropertyValuesJson);
            cmd.Parameters.AddWithValue("@livelymetadata", item.LivelyInfoMetadataJson);

            cmd.ExecuteNonQuery();
        }

        public List<WallpaperItem> GetAllWallpapers()
        {
            var list = new List<WallpaperItem>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM wallpapers ORDER BY dateadded DESC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadWallpaper(reader));
            }
            return list;
        }

        public WallpaperItem? GetWallpaperById(string id)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM wallpapers WHERE id = @id LIMIT 1;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadWallpaper(reader);
            }
            return null;
        }

        public bool DeleteWallpaper(string id)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM wallpapers WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        private static WallpaperItem ReadWallpaper(SqliteDataReader reader)
        {
            return new WallpaperItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Author = reader.IsDBNull(3) ? "" : reader.GetString(3),
                Version = reader.IsDBNull(4) ? "1.0.0" : reader.GetString(4),
                Contact = reader.IsDBNull(5) ? "" : reader.GetString(5),
                License = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Type = (WallpaperType)reader.GetInt32(7),
                FilePath = reader.GetString(8),
                PreviewPath = reader.IsDBNull(9) ? "" : reader.GetString(9),
                ThumbnailPath = reader.IsDBNull(10) ? "" : reader.GetString(10),
                Tags = reader.IsDBNull(11) ? "" : reader.GetString(11),
                Category = reader.IsDBNull(12) ? "General" : reader.GetString(12),
                IsFavorite = reader.GetInt32(13) == 1,
                DateAdded = DateTime.Parse(reader.GetString(14)),
                LastUsed = DateTime.Parse(reader.GetString(15)),
                SourceUrl = reader.IsDBNull(16) ? "" : reader.GetString(16),
                Arguments = reader.IsDBNull(17) ? "" : reader.GetString(17),
                CustomPropertiesJson = reader.IsDBNull(18) ? "" : reader.GetString(18),
                PropertyValuesJson = reader.IsDBNull(19) ? "" : reader.GetString(19),
                LivelyInfoMetadataJson = reader.IsDBNull(20) ? "" : reader.GetString(20)
            };
        }

        #endregion

        #region Monitor Assignments

        public void SaveMonitorAssignment(MonitorAssignment assignment)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT OR REPLACE INTO monitor_assignments (
                    monitor_device_id, monitor_index, wallpaper_id, mode, state,
                    volume, is_muted, speed, scaling, assigned_at
                ) VALUES (
                    @mid, @mindex, @wid, @mode, @state,
                    @volume, @ismuted, @speed, @scaling, @assignedat
                );
            ";

            cmd.Parameters.AddWithValue("@mid", assignment.MonitorDeviceId);
            cmd.Parameters.AddWithValue("@mindex", assignment.MonitorIndex);
            cmd.Parameters.AddWithValue("@wid", assignment.WallpaperId);
            cmd.Parameters.AddWithValue("@mode", (int)assignment.Mode);
            cmd.Parameters.AddWithValue("@state", (int)assignment.State);
            cmd.Parameters.AddWithValue("@volume", assignment.Volume);
            cmd.Parameters.AddWithValue("@ismuted", assignment.IsMuted ? 1 : 0);
            cmd.Parameters.AddWithValue("@speed", assignment.PlaybackSpeed);
            cmd.Parameters.AddWithValue("@scaling", (int)assignment.Scaling);
            cmd.Parameters.AddWithValue("@assignedat", assignment.AssignedAt.ToString("O"));

            cmd.ExecuteNonQuery();
        }

        public List<MonitorAssignment> GetMonitorAssignments()
        {
            var list = new List<MonitorAssignment>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM monitor_assignments;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new MonitorAssignment
                {
                    MonitorDeviceId = reader.GetString(0),
                    MonitorIndex = reader.GetInt32(1),
                    WallpaperId = reader.GetString(2),
                    Mode = (AssignmentMode)reader.GetInt32(3),
                    State = (PlaybackState)reader.GetInt32(4),
                    Volume = reader.GetInt32(5),
                    IsMuted = reader.GetInt32(6) == 1,
                    PlaybackSpeed = reader.GetDouble(7),
                    Scaling = (WallpaperScaling)reader.GetInt32(8),
                    AssignedAt = DateTime.Parse(reader.GetString(9))
                });
            }
            return list;
        }

        public void ClearMonitorAssignment(string monitorDeviceId)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM monitor_assignments WHERE monitor_device_id = @mid;";
            cmd.Parameters.AddWithValue("@mid", monitorDeviceId);
            cmd.ExecuteNonQuery();
        }

        public void ClearAllMonitorAssignments()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM monitor_assignments;";
            cmd.ExecuteNonQuery();
        }

        #endregion

        #region Playlists

        public void SavePlaylist(Playlist playlist)
        {
            using var conn = CreateConnection();
            using var trans = conn.BeginTransaction();

            using var cmd = conn.CreateCommand();
            cmd.Transaction = trans;
            cmd.CommandText = @"
                INSERT OR REPLACE INTO playlists (id, name, rotation_interval, is_shuffle, is_repeat, is_active, target_monitor, created_at)
                VALUES (@id, @name, @interval, @shuffle, @repeat, @active, @target, @created);
            ";
            cmd.Parameters.AddWithValue("@id", playlist.Id);
            cmd.Parameters.AddWithValue("@name", playlist.Name);
            cmd.Parameters.AddWithValue("@interval", playlist.RotationIntervalSeconds);
            cmd.Parameters.AddWithValue("@shuffle", playlist.IsShuffle ? 1 : 0);
            cmd.Parameters.AddWithValue("@repeat", playlist.IsRepeat ? 1 : 0);
            cmd.Parameters.AddWithValue("@active", playlist.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@target", playlist.TargetMonitorIndex);
            cmd.Parameters.AddWithValue("@created", playlist.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();

            // Clear old items
            using var delCmd = conn.CreateCommand();
            delCmd.Transaction = trans;
            delCmd.CommandText = "DELETE FROM playlist_items WHERE playlist_id = @pid;";
            delCmd.Parameters.AddWithValue("@pid", playlist.Id);
            delCmd.ExecuteNonQuery();

            // Insert new items
            foreach (var item in playlist.Items)
            {
                using var itemCmd = conn.CreateCommand();
                itemCmd.Transaction = trans;
                itemCmd.CommandText = @"
                    INSERT INTO playlist_items (id, playlist_id, wallpaper_id, order_index, custom_duration)
                    VALUES (@id, @pid, @wid, @order, @duration);
                ";
                itemCmd.Parameters.AddWithValue("@id", item.Id);
                itemCmd.Parameters.AddWithValue("@pid", playlist.Id);
                itemCmd.Parameters.AddWithValue("@wid", item.WallpaperId);
                itemCmd.Parameters.AddWithValue("@order", item.OrderIndex);
                itemCmd.Parameters.AddWithValue("@duration", item.CustomDurationSeconds);
                itemCmd.ExecuteNonQuery();
            }

            trans.Commit();
        }

        public List<Playlist> GetAllPlaylists()
        {
            var list = new List<Playlist>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM playlists;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var p = new Playlist
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    RotationIntervalSeconds = reader.GetInt32(2),
                    IsShuffle = reader.GetInt32(3) == 1,
                    IsRepeat = reader.GetInt32(4) == 1,
                    IsActive = reader.GetInt32(5) == 1,
                    TargetMonitorIndex = reader.GetInt32(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7))
                };
                list.Add(p);
            }

            foreach (var p in list)
            {
                using var itemCmd = conn.CreateCommand();
                itemCmd.CommandText = "SELECT * FROM playlist_items WHERE playlist_id = @pid ORDER BY order_index ASC;";
                itemCmd.Parameters.AddWithValue("@pid", p.Id);
                using var itemReader = itemCmd.ExecuteReader();
                while (itemReader.Read())
                {
                    p.Items.Add(new PlaylistItem
                    {
                        Id = itemReader.GetString(0),
                        PlaylistId = itemReader.GetString(1),
                        WallpaperId = itemReader.GetString(2),
                        OrderIndex = itemReader.GetInt32(3),
                        CustomDurationSeconds = itemReader.GetInt32(4)
                    });
                }
            }

            return list;
        }

        public bool DeletePlaylist(string playlistId)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM playlists WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", playlistId);
            return cmd.ExecuteNonQuery() > 0;
        }

        #endregion

        #region Application Rules

        public void SaveApplicationRule(ApplicationRule rule)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO application_rules (id, process_name, action, description, is_enabled)
                VALUES (@id, @proc, @act, @desc, @enabled);
            ";
            cmd.Parameters.AddWithValue("@id", rule.Id);
            cmd.Parameters.AddWithValue("@proc", rule.ProcessName.ToLowerInvariant());
            cmd.Parameters.AddWithValue("@act", (int)rule.Action);
            cmd.Parameters.AddWithValue("@desc", rule.Description);
            cmd.Parameters.AddWithValue("@enabled", rule.IsEnabled ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public List<ApplicationRule> GetAllApplicationRules()
        {
            var list = new List<ApplicationRule>();
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM application_rules;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ApplicationRule
                {
                    Id = reader.GetString(0),
                    ProcessName = reader.GetString(1),
                    Action = (RuleAction)reader.GetInt32(2),
                    Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    IsEnabled = reader.GetInt32(4) == 1
                });
            }
            return list;
        }

        public bool DeleteApplicationRule(string id)
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM application_rules WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        #endregion

        #region Settings

        public void SaveSettings(AuraSettings settings)
        {
            string json = JsonSerializer.Serialize(settings);
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ('general_settings', @json);";
            cmd.Parameters.AddWithValue("@json", json);
            cmd.ExecuteNonQuery();
        }

        public AuraSettings LoadSettings()
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM settings WHERE key = 'general_settings' LIMIT 1;";

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                string json = reader.GetString(0);
                return JsonSerializer.Deserialize<AuraSettings>(json) ?? new AuraSettings();
            }
            return new AuraSettings();
        }

        #endregion

        public void Dispose()
        {
            // SQLite in shared cache mode handles connections
        }
    }
}
