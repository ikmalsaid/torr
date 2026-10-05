using System;
using System.Collections.Generic;
using System.IO;

namespace TorrentViewer
{
    public class TorrentMetadata
    {
        public string Name { get; set; }
        public string InfoHash { get; set; }
        public string MagnetUri { get; set; }
        public long TotalSize { get; set; }
        public long PieceLength { get; set; }
        public int PieceCount { get; set; }
        public bool IsPrivate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreationDate { get; set; }
        public string Comment { get; set; }
        public string PrimaryTracker { get; set; }
        public List<string> Trackers { get; set; }
        public int TotalFilesCount { get; set; }
        public int TotalFoldersCount { get; set; }
        public TorrentDirectory RootDirectory { get; set; }
        public List<TorrentFile> AllFiles { get; set; }

        public TorrentMetadata()
        {
            Name = string.Empty;
            InfoHash = string.Empty;
            MagnetUri = string.Empty;
            CreatedBy = string.Empty;
            Comment = string.Empty;
            PrimaryTracker = string.Empty;
            Trackers = new List<string>();
            AllFiles = new List<TorrentFile>();
        }

        public string FormattedTotalSize
        {
            get { return FormatBytes(TotalSize); }
        }

        public string FormattedPieceLength
        {
            get { return FormatBytes(PieceLength); }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return Strings.FormatBytesZero;
            if (bytes < 1024) return bytes + " " + Strings.UnitBytes;
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.##") + " " + Strings.UnitKiloBytes;
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.##") + " " + Strings.UnitMegaBytes;
            double gb = mb / 1024.0;
            if (gb < 1024) return gb.ToString("0.##") + " " + Strings.UnitGigaBytes;
            double tb = gb / 1024.0;
            return tb.ToString("0.##") + " " + Strings.UnitTeraBytes;
        }
    }

    public class TorrentFile
    {
        public string Name { get; set; }
        public string RelativePath { get; set; }
        public long Length { get; set; }
        public long Offset { get; set; }
        public int StartPiece { get; set; }
        public int EndPiece { get; set; }
        public int PieceCount { get; set; }
        public string Extension { get; set; }
        public string TypeDescription { get; set; }
        public TorrentDirectory ParentDirectory { get; set; }

        public TorrentFile(string name, string relativePath, long length, long offset, long pieceLength)
        {
            Name = name;
            RelativePath = relativePath;
            Length = length;
            Offset = offset;

            string ext = Path.GetExtension(name);
            Extension = string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();

            if (pieceLength > 0)
            {
                StartPiece = (int)(offset / pieceLength);
                if (length > 0)
                {
                    EndPiece = (int)((offset + length - 1) / pieceLength);
                    PieceCount = (EndPiece - StartPiece) + 1;
                }
                else
                {
                    EndPiece = StartPiece;
                    PieceCount = 0;
                }
            }
            else
            {
                StartPiece = 0;
                EndPiece = 0;
                PieceCount = 0;
            }
        }

        public string FormattedSize
        {
            get { return TorrentMetadata.FormatBytes(Length); }
        }

        public string FormattedOffset
        {
            get { return TorrentMetadata.FormatBytes(Offset); }
        }

        /// <summary>
        /// Returns relative path omitting the root directory name.
        /// For example, "root/folder/file" becomes "folder/file", and a file directly in root returns just its name.
        /// </summary>
        public string GetRelativePathWithoutRoot()
        {
            if (ParentDirectory == null || ParentDirectory.Parent == null)
            {
                return Name;
            }

            List<string> segments = new List<string>();
            TorrentDirectory cur = ParentDirectory;
            while (cur != null && cur.Parent != null)
            {
                segments.Insert(0, cur.Name);
                cur = cur.Parent;
            }
            segments.Add(Name);
            return string.Join("\\", segments.ToArray());
        }
    }

    public class TorrentDirectory
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public TorrentDirectory Parent { get; set; }
        public List<TorrentDirectory> SubDirectories { get; set; }
        public List<TorrentFile> Files { get; set; }
        public long TotalSize { get; set; }
        public int TotalFilesCount { get; set; }
        public int TotalFoldersCount { get; set; }

        private List<TorrentItem> _cachedItems;

        public TorrentDirectory(string name, TorrentDirectory parent)
        {
            Name = name;
            Parent = parent;
            SubDirectories = new List<TorrentDirectory>();
            Files = new List<TorrentFile>();

            if (parent == null)
            {
                FullPath = name;
            }
            else
            {
                FullPath = parent.FullPath + "\\" + name;
            }
        }

        public TorrentDirectory GetOrCreateSubDirectory(string subDirName)
        {
            for (int i = 0; i < SubDirectories.Count; i++)
            {
                if (string.Equals(SubDirectories[i].Name, subDirName, StringComparison.OrdinalIgnoreCase))
                {
                    return SubDirectories[i];
                }
            }

            TorrentDirectory newSubDir = new TorrentDirectory(subDirName, this);
            SubDirectories.Add(newSubDir);
            return newSubDir;
        }

        public void CalculateSizesAndCounts()
        {
            long size = 0;
            int fileCount = Files.Count;
            int folderCount = SubDirectories.Count;

            for (int i = 0; i < Files.Count; i++)
            {
                size += Files[i].Length;
            }

            for (int i = 0; i < SubDirectories.Count; i++)
            {
                SubDirectories[i].CalculateSizesAndCounts();
                size += SubDirectories[i].TotalSize;
                fileCount += SubDirectories[i].TotalFilesCount;
                folderCount += SubDirectories[i].TotalFoldersCount;
            }

            TotalSize = size;
            TotalFilesCount = fileCount;
            TotalFoldersCount = folderCount;
            _cachedItems = null; // Invalidate cached items
        }

        public string FormattedTotalSize
        {
            get { return TorrentMetadata.FormatBytes(TotalSize); }
        }

        /// <summary>
        /// Retrieves cached list of TorrentItems for FileView display.
        /// Includes '..' parent folder row if this directory has a parent.
        /// </summary>
        public List<TorrentItem> GetDisplayItems()
        {
            if (_cachedItems != null)
            {
                return new List<TorrentItem>(_cachedItems);
            }

            List<TorrentItem> items = new List<TorrentItem>();

            // 1. Parent folder '..'
            if (Parent != null)
            {
                TorrentItem parentItem = new TorrentItem();
                parentItem.IsParentFolder = true;
                parentItem.Name = Strings.ItemParentName;
                parentItem.Size = 0;
                parentItem.SizeDisplay = string.Empty;
                parentItem.TypeDisplay = Strings.TypeParentFolder;
                parentItem.PieceRangeDisplay = string.Empty;
                parentItem.PathDisplay = Parent.FullPath;
                parentItem.Directory = Parent;
                items.Add(parentItem);
            }

            // 2. Subdirectories
            for (int i = 0; i < SubDirectories.Count; i++)
            {
                TorrentDirectory sub = SubDirectories[i];
                TorrentItem folderItem = new TorrentItem();
                folderItem.IsFolder = true;
                folderItem.Name = sub.Name;
                folderItem.Size = sub.TotalSize;
                folderItem.SizeDisplay = TorrentMetadata.FormatBytes(sub.TotalSize);
                folderItem.TypeDisplay = Strings.TypeFileFolder;
                folderItem.PieceRangeDisplay = string.Format(Strings.FormatFilesCountShort, sub.TotalFilesCount);
                folderItem.PathDisplay = sub.FullPath;
                folderItem.Directory = sub;
                items.Add(folderItem);
            }

            // 3. Files in this directory
            for (int i = 0; i < Files.Count; i++)
            {
                TorrentFile file = Files[i];
                TorrentItem fileItem = new TorrentItem();
                fileItem.IsFile = true;
                fileItem.Name = file.Name;
                fileItem.Size = file.Length;
                fileItem.SizeDisplay = TorrentMetadata.FormatBytes(file.Length);
                fileItem.TypeDisplay = string.IsNullOrEmpty(file.TypeDescription) ? Strings.TypeDefaultFile : file.TypeDescription;
                fileItem.PieceRangeDisplay = file.PieceCount > 0
                    ? string.Format(Strings.FormatShortPieceRange, file.StartPiece, file.EndPiece, file.PieceCount)
                    : Strings.ValueZeroPieces;
                fileItem.PathDisplay = file.RelativePath;
                fileItem.File = file;
                items.Add(fileItem);
            }

            _cachedItems = items;
            return new List<TorrentItem>(_cachedItems);
        }
    }

    public class TorrentItem
    {
        public bool IsParentFolder { get; set; }
        public bool IsFolder { get; set; }
        public bool IsFile { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string SizeDisplay { get; set; }
        public string TypeDisplay { get; set; }
        public string PieceRangeDisplay { get; set; }
        public string PathDisplay { get; set; }
        public int ImageIndex { get; set; }
        public TorrentFile File { get; set; }
        public TorrentDirectory Directory { get; set; }

        public TorrentItem()
        {
            Name = string.Empty;
            SizeDisplay = string.Empty;
            TypeDisplay = string.Empty;
            PieceRangeDisplay = string.Empty;
            PathDisplay = string.Empty;
        }
    }

    /// <summary>
    /// Custom comparer for column sorting in Virtual ListView.
    /// Guarantees that '..' remains at index 0, and folders stay grouped before files.
    /// </summary>
    public class TorrentItemComparer : IComparer<TorrentItem>
    {
        private readonly int _columnIndex;
        private readonly bool _ascending;

        public TorrentItemComparer(int columnIndex, bool ascending)
        {
            _columnIndex = columnIndex;
            _ascending = ascending;
        }

        public int Compare(TorrentItem x, TorrentItem y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // '..' parent folder ALWAYS stays at the very top
            if (x.IsParentFolder) return -1;
            if (y.IsParentFolder) return 1;

            // Folders always precede files (like Windows Explorer)
            if (x.IsFolder && !y.IsFolder) return -1;
            if (!x.IsFolder && y.IsFolder) return 1;

            int cmp = 0;
            switch (_columnIndex)
            {
                case 0: // Name
                    cmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    break;
                case 1: // Size
                    cmp = x.Size.CompareTo(y.Size);
                    break;
                case 2: // Type
                    cmp = string.Compare(x.TypeDisplay, y.TypeDisplay, StringComparison.OrdinalIgnoreCase);
                    break;
                case 3: // Piece Range / Details
                    if (x.IsFile && y.IsFile)
                    {
                        cmp = x.File.StartPiece.CompareTo(y.File.StartPiece);
                    }
                    else
                    {
                        cmp = string.Compare(x.PieceRangeDisplay, y.PieceRangeDisplay, StringComparison.OrdinalIgnoreCase);
                    }
                    break;
                case 4: // Path
                    cmp = string.Compare(x.PathDisplay, y.PathDisplay, StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    cmp = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                    break;
            }

            return _ascending ? cmp : -cmp;
        }
    }
}
