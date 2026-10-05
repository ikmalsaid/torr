using System;

namespace TorrentViewer
{
    /// <summary>
    /// Centralized repository for all user-facing strings, labels, menu items,
    /// dialog messages, and display format strings across the application.
    /// </summary>
    public static class Strings
    {
        #region Application & About

        public const string AppName = "Torr";
        public const string AppTitleLoaded = "{0} - Torr";
        public const string AppVersion = "Version 1.0";
        public const string AboutDescription = "A lightweight BitTorrent metadata and file viewer.";
        public const string AboutMessage = "Torr\nVersion 1.0\n\nA lightweight BitTorrent metadata and file viewer.";
        public const string AboutTitle = "About Torr";
        public const string AboutMenuItem = "&About Torr...";

        #endregion

        #region Menu - File

        public const string MenuFile = "&File";
        public const string MenuOpenTorrent = "&Open Torrent...";
        public const string MenuCloseTorrent = "&Close Torrent";
        public const string MenuCopyMagnet = "Copy &Magnet URI";
        public const string MenuCopyInfoHash = "Copy &Info Hash (Hex)";
        public const string MenuExit = "E&xit";

        #endregion

        #region Menu - Edit

        public const string MenuEdit = "&Edit";
        public const string MenuCopyItemName = "&Copy Item Name";
        public const string MenuCopyFileSize = "Copy &File Size";
        public const string MenuSelectAll = "Select &All";

        #endregion

        #region Menu - View

        public const string MenuView = "&View";
        public const string MenuGoToParent = "Go to &Parent Folder (..)";
        public const string MenuRefresh = "&Refresh";
        public const string MenuExpandAll = "&Expand All Folders";
        public const string MenuCollapseAll = "&Collapse All Folders";
        public const string MenuShowLeftSidebar = "Show Torrent &Info Sidebar";
        public const string MenuShowRightSidebar = "Show File &Details Sidebar";
        public const string MenuMaximizeFilesView = "&Maximize Files View (Hide Sidebars)";
        public const string MenuAutoDetectTheme = "&Auto-Detect Theme (Follow Windows)";
        public const string MenuDarkMode = "&Dark Mode";
        public const string MenuResetLayout = "Reset &Layout (20% / 80%)";

        #endregion

        #region Menu - Help

        public const string MenuHelp = "&Help";

        #endregion

        #region Context Menu

        public const string CtxCopyItemName = "Copy Item Name";
        public const string CtxCopyFileSize = "Copy File Size";
        public const string CtxGoToParent = "Go to Parent Folder (..)";

        #endregion

        #region ListView Columns

        public const string ColName = "Name";
        public const string ColSize = "Size";
        public const string ColType = "Type";
        public const string ColPieces = "Pieces";

        #endregion

        #region Address Bar & Navigation

        public const string AddressTitle = "Address";
        public const string RootPathBackslash = "\\";
        public const string FolderSummaryFormat = "{0} files, {1} folders  •  {2}";
        public const string FolderSummaryEmpty = "0 items";

        #endregion

        #region Section Titles

        public const string SectionDirectories = "Directories";
        public const string SectionTorrentInfo = "Torrent Information";
        public const string SectionSelectedItemDetails = "Selected Item Details";

        #endregion

        #region Left Sidebar - General Information

        public const string GroupGeneralInfo = "General Information";
        public const string LabelTorrentName = "Torrent Name:";
        public const string LabelInfoHash = "Info Hash (SHA-1):";
        public const string LabelTotalSize = "Total Size:";
        public const string LabelContentCount = "Content Count:";
        public const string LabelPieceLength = "Piece Length:";
        public const string LabelPiecesCount = "Pieces Count:";
        public const string LabelPrivacy = "Privacy:";
        public const string ValuePrivateTracker = "Private Tracker";
        public const string ValuePublicTorrent = "Public Torrent";

        #endregion

        #region Left Sidebar - Metadata

        public const string GroupMetadata = "Metadata";
        public const string LabelCreatedBy = "Created By:";
        public const string LabelDateCreated = "Date Created:";
        public const string LabelComment = "Comment:";
        public const string ValueNotSpecified = "Not specified";
        public const string ValueNone = "(None)";

        #endregion

        #region Left Sidebar - Network & Trackers

        public const string GroupTrackers = "Network & Trackers";
        public const string LabelPrimaryTracker = "Primary Tracker URL:";
        public const string LabelAllTrackers = "All Trackers / Announce List:";
        public const string ValueNoTrackers = "(No trackers specified in torrent)";

        #endregion

        #region Right Sidebar - Selection

        public const string GroupSelection = "Selection";
        public const string SelectionPlaceholder = "Select a file or folder in the list to view its properties.";
        public const string SelectionNoSelection = "No Selection";
        public const string SelectionParentDirectory = ".. (Parent Directory)";
        public const string SelectionFolderNavigation = "Folder Navigation";
        public const string SelectionDirectoryHeader = "Directory  •  {0}";
        public const string SelectionCurrentDirectoryHeader = "Current Directory  •  {0}";
        public const string SelectionFileHeader = "{0}  •  {1}";
        public const string SelectionMultiple = "Multiple Selection";
        public const string SelectionMultipleCount = "{0} items selected";

        #endregion

        #region Right Sidebar - Properties

        public const string GroupProperties = "Properties";
        public const string LabelSize = "Size:";
        public const string LabelFileType = "File Type:";
        public const string LabelExtension = "Extension:";
        public const string LabelFileOffset = "File Offset:";
        public const string LabelPieceRange = "Piece Range:";
        public const string LabelTorrentShare = "Torrent Share:";

        #endregion

        #region Display Types & Values

        public const string ItemParentName = "..";
        public const string TypeParentFolder = "Parent folder";
        public const string TypeFileFolder = "File folder";
        public const string TypeMultipleItems = "Multiple items";
        public const string TypeDefaultFile = "File";
        public const string TypeDefaultFolder = "Folder";
        public const string DefaultDash = "-";
        public const string ValueZeroPieces = "0 pieces";
        public const string ValueUnnamedTorrent = "Unnamed Torrent";
        public const string ButtonCopy = "Copy";

        #endregion

        #region Format Strings

        public const string FormatSizeWithBytes = "{0}  ({1:N0} bytes)";
        public const string FormatPiecesCount = "{0:N0} pieces";
        public const string FormatContentCount = "{0} files, {1} folders";
        public const string FormatDirectoryItems = "{0} files, {1} subfolders";
        public const string FormatPiecesSpanned = "Pieces {0} to {1}  ({2} pieces spanned)";
        public const string FormatTorrentShare = "{0:0.##}% of total torrent";
        public const string FormatDateTime = "yyyy-MM-dd HH:mm:ss";
        public const string FormatShortPieceRange = "{0} - {1} ({2} pcs)";
        public const string FormatFilesCountShort = "{0} files";

        #endregion

        #region Dialogs & File Filters

        public const string TorrentFileExtension = ".torrent";
        public const string OpenTorrentFilter = "BitTorrent Files (*.torrent)|*.torrent|All Files (*.*)|*.*";
        public const string OpenTorrentTitle = "Open BitTorrent File";
        public const string TorrentParseErrorTitle = "Torrent Parse Error";
        public const string TorrentParseErrorMessage = "Failed to load torrent file:\n\n{0}";

        #endregion

        #region File Size Units

        public const string UnitBytes = "B";
        public const string UnitKiloBytes = "KB";
        public const string UnitMegaBytes = "MB";
        public const string UnitGigaBytes = "GB";
        public const string UnitTeraBytes = "TB";
        public const string FormatBytesZero = "0 B";

        #endregion
    }
}
