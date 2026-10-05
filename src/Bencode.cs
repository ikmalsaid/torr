using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TorrentViewer
{
    /// <summary>
    /// Fast, low-allocation Bencode parser and SHA-1 InfoHash calculator.
    /// Operates directly on byte buffers without unnecessary intermediate allocations.
    /// </summary>
    public class BencodeParser
    {
        private readonly byte[] _data;
        private int _index;

        public BencodeParser(byte[] data)
        {
            if (data == null) throw new ArgumentNullException("data");
            _data = data;
            _index = 0;
        }

        public object Parse()
        {
            if (_index >= _data.Length) return null;
            byte b = _data[_index];

            if (b == 'i') return ParseInteger();
            if (b == 'l') return ParseList();
            if (b == 'd') return ParseDictionary();
            if (b >= '0' && b <= '9') return ParseString();

            throw new FormatException(string.Format("Invalid Bencode token '{0}' at byte offset {1}", (char)b, _index));
        }

        public long ParseInteger()
        {
            _index++; // Skip 'i'
            int start = _index;
            while (_index < _data.Length && _data[_index] != 'e')
            {
                _index++;
            }
            if (_index >= _data.Length)
            {
                throw new FormatException("Unterminated integer token in Bencode stream.");
            }

            string numStr = Encoding.ASCII.GetString(_data, start, _index - start);
            _index++; // Skip 'e'

            long result;
            if (!long.TryParse(numStr, out result))
            {
                throw new FormatException(string.Format("Invalid integer value: {0}", numStr));
            }
            return result;
        }

        public byte[] ParseString()
        {
            int start = _index;
            while (_index < _data.Length && _data[_index] != ':')
            {
                _index++;
            }
            if (_index >= _data.Length)
            {
                throw new FormatException("Unterminated string length prefix in Bencode stream.");
            }

            string lenStr = Encoding.ASCII.GetString(_data, start, _index - start);
            int len;
            if (!int.TryParse(lenStr, out len) || len < 0)
            {
                throw new FormatException(string.Format("Invalid string length prefix: {0}", lenStr));
            }

            _index++; // Skip ':'
            if (_index + len > _data.Length)
            {
                throw new FormatException("String length exceeds available buffer size.");
            }

            byte[] strBytes = new byte[len];
            Buffer.BlockCopy(_data, _index, strBytes, 0, len);
            _index += len;
            return strBytes;
        }

        public List<object> ParseList()
        {
            _index++; // Skip 'l'
            List<object> list = new List<object>();
            while (_index < _data.Length && _data[_index] != 'e')
            {
                list.Add(Parse());
            }
            if (_index >= _data.Length)
            {
                throw new FormatException("Unterminated list in Bencode stream.");
            }
            _index++; // Skip 'e'
            return list;
        }

        public Dictionary<string, object> ParseDictionary()
        {
            int start, end;
            return ParseDictionary(out start, out end);
        }

        public Dictionary<string, object> ParseDictionary(out int dictStart, out int dictEnd)
        {
            dictStart = _index;
            _index++; // Skip 'd'
            Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            while (_index < _data.Length && _data[_index] != 'e')
            {
                byte[] keyBytes = ParseString();
                string key = Encoding.UTF8.GetString(keyBytes);

                if (string.Equals(key, "info", StringComparison.OrdinalIgnoreCase))
                {
                    int infoStart = _index;
                    int infoEnd;
                    object val = ParseDictionary(out infoStart, out infoEnd);
                    dict[key] = val;
                    dict["__raw_info_start"] = infoStart;
                    dict["__raw_info_end"] = infoEnd;
                }
                else
                {
                    dict[key] = Parse();
                }
            }

            if (_index >= _data.Length)
            {
                throw new FormatException("Unterminated dictionary in Bencode stream.");
            }

            _index++; // Skip 'e'
            dictEnd = _index;
            return dict;
        }

        /// <summary>
        /// Reads a torrent file and parses it into a strongly typed TorrentMetadata object.
        /// Computes the exact 40-character SHA-1 InfoHash over the raw bencoded 'info' dictionary slice.
        /// </summary>
        public static TorrentMetadata LoadFromFile(string filePath)
        {
            byte[] rawBytes = File.ReadAllBytes(filePath);
            return LoadFromBytes(rawBytes);
        }

        public static TorrentMetadata LoadFromBytes(byte[] rawBytes)
        {
            BencodeParser parser = new BencodeParser(rawBytes);
            int rootStart, rootEnd;
            Dictionary<string, object> root = parser.ParseDictionary(out rootStart, out rootEnd);

            if (!root.ContainsKey("info"))
            {
                throw new FormatException("Invalid torrent file: missing 'info' dictionary.");
            }

            Dictionary<string, object> info = root["info"] as Dictionary<string, object>;
            if (info == null)
            {
                throw new FormatException("Invalid torrent file: 'info' is not a dictionary.");
            }

            int infoStart = (int)root["__raw_info_start"];
            int infoEnd = (int)root["__raw_info_end"];
            int infoLen = infoEnd - infoStart;

            // Compute exact SHA-1 InfoHash over raw bencoded info dictionary bytes
            string infoHashHex;
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hashBytes = sha1.ComputeHash(rawBytes, infoStart, infoLen);
                StringBuilder sb = new StringBuilder(40);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                infoHashHex = sb.ToString();
            }

            TorrentMetadata metadata = new TorrentMetadata();
            metadata.InfoHash = infoHashHex;

            // Announce & Announce List
            if (root.ContainsKey("announce"))
            {
                metadata.PrimaryTracker = GetStringValue(root["announce"]);
                if (!string.IsNullOrEmpty(metadata.PrimaryTracker))
                {
                    metadata.Trackers.Add(metadata.PrimaryTracker);
                }
            }

            if (root.ContainsKey("announce-list"))
            {
                List<object> tiers = root["announce-list"] as List<object>;
                if (tiers != null)
                {
                    for (int i = 0; i < tiers.Count; i++)
                    {
                        List<object> tier = tiers[i] as List<object>;
                        if (tier != null)
                        {
                            for (int j = 0; j < tier.Count; j++)
                            {
                                string url = GetStringValue(tier[j]);
                                if (!string.IsNullOrEmpty(url) && !metadata.Trackers.Contains(url))
                                {
                                    metadata.Trackers.Add(url);
                                }
                            }
                        }
                    }
                }
            }

            // Comment
            if (root.ContainsKey("comment"))
            {
                metadata.Comment = GetStringValue(root["comment"]);
            }

            // Created By
            if (root.ContainsKey("created by"))
            {
                metadata.CreatedBy = GetStringValue(root["created by"]);
            }

            // Creation Date
            if (root.ContainsKey("creation date"))
            {
                long unixSec = Convert.ToInt64(root["creation date"]);
                try
                {
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    metadata.CreationDate = epoch.AddSeconds(unixSec).ToLocalTime();
                }
                catch
                {
                    metadata.CreationDate = null;
                }
            }

            // Info dictionary fields:
            // Name:
            string torrentName = null;
            if (info.ContainsKey("name.utf-8"))
            {
                torrentName = GetStringValue(info["name.utf-8"]);
            }
            if (string.IsNullOrEmpty(torrentName) && info.ContainsKey("name"))
            {
                torrentName = GetStringValue(info["name"]);
            }
            metadata.Name = string.IsNullOrEmpty(torrentName) ? Strings.ValueUnnamedTorrent : torrentName;

            // Piece Length:
            if (info.ContainsKey("piece length"))
            {
                metadata.PieceLength = Convert.ToInt64(info["piece length"]);
            }
            else
            {
                metadata.PieceLength = 262144; // Default 256 KB if not specified
            }

            // Pieces hash buffer count:
            if (info.ContainsKey("pieces"))
            {
                byte[] pieces = info["pieces"] as byte[];
                if (pieces != null)
                {
                    metadata.PieceCount = pieces.Length / 20;
                }
            }

            // Private flag:
            if (info.ContainsKey("private"))
            {
                metadata.IsPrivate = Convert.ToInt64(info["private"]) == 1;
            }

            // Single file vs multi file:
            metadata.RootDirectory = new TorrentDirectory(metadata.Name, null);
            long runningOffset = 0;

            if (info.ContainsKey("files"))
            {
                // Multi-file torrent
                List<object> filesList = info["files"] as List<object>;
                if (filesList != null)
                {
                    for (int f = 0; f < filesList.Count; f++)
                    {
                        Dictionary<string, object> fileDict = filesList[f] as Dictionary<string, object>;
                        if (fileDict == null) continue;

                        long fileLen = 0;
                        if (fileDict.ContainsKey("length"))
                        {
                            fileLen = Convert.ToInt64(fileDict["length"]);
                        }

                        // Path components
                        List<string> pathParts = new List<string>();
                        List<object> pathObjs = null;

                        if (fileDict.ContainsKey("path.utf-8"))
                        {
                            pathObjs = fileDict["path.utf-8"] as List<object>;
                        }
                        if (pathObjs == null && fileDict.ContainsKey("path"))
                        {
                            pathObjs = fileDict["path"] as List<object>;
                        }

                        if (pathObjs != null)
                        {
                            for (int p = 0; p < pathObjs.Count; p++)
                            {
                                string seg = GetStringValue(pathObjs[p]);
                                if (!string.IsNullOrEmpty(seg))
                                {
                                    pathParts.Add(seg);
                                }
                            }
                        }

                        if (pathParts.Count == 0)
                        {
                            pathParts.Add(string.Format("file_{0}", f + 1));
                        }

                        // If torrent path explicitly starts with the root folder name, omit it
                        if (pathParts.Count > 1 && string.Equals(pathParts[0], metadata.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            pathParts.RemoveAt(0);
                        }

                        // Place in directory hierarchy
                        TorrentDirectory currentDir = metadata.RootDirectory;
                        for (int i = 0; i < pathParts.Count - 1; i++)
                        {
                            string dirName = pathParts[i];
                            currentDir = currentDir.GetOrCreateSubDirectory(dirName);
                        }

                        string fileName = pathParts[pathParts.Count - 1];
                        string relPath = string.Join("\\", pathParts.ToArray());

                        TorrentFile torrentFile = new TorrentFile(fileName, relPath, fileLen, runningOffset, metadata.PieceLength);
                        currentDir.Files.Add(torrentFile);
                        torrentFile.ParentDirectory = currentDir;
                        torrentFile.RelativePath = torrentFile.GetRelativePathWithoutRoot();
                        metadata.AllFiles.Add(torrentFile);

                        runningOffset += fileLen;
                    }
                }
            }
            else
            {
                // Single-file torrent
                long fileLen = 0;
                if (info.ContainsKey("length"))
                {
                    fileLen = Convert.ToInt64(info["length"]);
                }

                TorrentFile singleFile = new TorrentFile(metadata.Name, metadata.Name, fileLen, 0, metadata.PieceLength);
                metadata.RootDirectory.Files.Add(singleFile);
                singleFile.ParentDirectory = metadata.RootDirectory;
                metadata.AllFiles.Add(singleFile);
                runningOffset = fileLen;
            }

            metadata.TotalSize = runningOffset;

            // Recalculate recursive sizes and folder/file counts
            metadata.RootDirectory.CalculateSizesAndCounts();
            metadata.TotalFilesCount = metadata.AllFiles.Count;
            metadata.TotalFoldersCount = metadata.RootDirectory.TotalFoldersCount;

            // Construct standard Magnet URI
            metadata.MagnetUri = BuildMagnetUri(metadata);

            return metadata;
        }

        private static string BuildMagnetUri(TorrentMetadata meta)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("magnet:?xt=urn:btih:").Append(meta.InfoHash);
            if (!string.IsNullOrEmpty(meta.Name))
            {
                sb.Append("&dn=").Append(Uri.EscapeDataString(meta.Name));
            }
            if (meta.TotalSize > 0)
            {
                sb.Append("&xl=").Append(meta.TotalSize);
            }
            for (int i = 0; i < meta.Trackers.Count; i++)
            {
                sb.Append("&tr=").Append(Uri.EscapeDataString(meta.Trackers[i]));
            }
            return sb.ToString();
        }

        private static string GetStringValue(object obj)
        {
            if (obj == null) return string.Empty;
            byte[] bytes = obj as byte[];
            if (bytes != null)
            {
                return Encoding.UTF8.GetString(bytes);
            }
            return obj.ToString();
        }
    }
}
