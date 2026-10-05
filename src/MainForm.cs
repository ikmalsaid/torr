using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace TorrentViewer
{
    public class MainForm : Form
    {
        // Data Model
        private TorrentMetadata _metadata;
        private TorrentDirectory _currentDirectory;
        private List<TorrentItem> _displayItems = new List<TorrentItem>();
        private readonly Dictionary<TorrentDirectory, TreeNode> _dirToNodeMap = new Dictionary<TorrentDirectory, TreeNode>();

        // Sorting & Layout State
        private int _sortColumn = 0;
        private bool _sortAscending = true;
        private bool _isAdjustingSplitters = false;
        private bool _userMovedCenterSplitter = false;

        // UI Components
        private MenuStrip _menuStrip;

        // SplitContainers (FixedPanel = None to eliminate InvalidOperationException completely)
        private SplitContainer _splitMain;             // Left: Torrent Info | Right: Center Area + Details
        private SplitContainer _splitCenterAndRight;   // Left: Tree+Files   | Right: File Details
        private SplitContainer _splitTreeAndFiles;     // Left: Tree (20%)   | Right: Files (80%)

        // TreeView & ListView
        private TreeView _treeDirectories;
        private ListView _lvFiles;
        private Panel _pnlBreadcrumb;
        private Panel _pnlAddressBox;
        private Label _lblBreadcrumb;
        private Label _lblFolderSummary;
        private PictureBox _picBreadcrumbIcon;

        // Left Sidebar Controls (Torrent Info)
        private Panel _pnlLeftContent;
        private TextBox _txtTorrentName;
        private TextBox _txtInfoHash;
        private Label _lblTotalSize;
        private Label _lblFilesCount;
        private Label _lblPieceSize;
        private Label _lblPiecesCount;
        private Label _lblPrivacy;
        private Label _lblCreatedBy;
        private Label _lblCreationDate;
        private TextBox _txtComment;
        private TextBox _txtPrimaryTracker;
        private ListBox _lstTrackers;

        // Right Sidebar Controls (Selected Item Details)
        private Panel _pnlRightContent;
        private PictureBox _picItemLargeIcon;
        private Label _lblItemHeaderName;
        private Label _lblItemHeaderType;
        private Label _lblItemSize;
        private Label _lblItemType;
        private Label _lblItemExt;
        private Label _lblItemOffset;
        private Label _lblItemPieceRange;
        private Label _lblItemShare;
        private Label _lblRightPlaceholder;

        // Menu items that toggle
        private ToolStripMenuItem _menuShowLeftSidebar;
        private ToolStripMenuItem _menuShowRightSidebar;

        // Theme State & Themed Controls
        private ThemePreference _themePreference = ThemePreference.Auto;
        private bool _isDarkMode = false;
        private ToolStripMenuItem _menuThemeAuto;
        private ToolStripMenuItem _menuDarkMode;
        private Panel _clientContainer;
        private Label _lblAddressTitle;
        private Button _btnCopyHash;
        private GroupBox _grpSelection;

        private readonly List<Panel> _sectionHeaderPanels = new List<Panel>();
        private readonly List<Label> _sectionHeaderLabels = new List<Label>();
        private readonly List<GroupBox> _allGroupBoxes = new List<GroupBox>();
        private readonly List<Label> _propertyTitleLabels = new List<Label>();
        private readonly List<Label> _propertyValueLabels = new List<Label>();
        private readonly List<TextBox> _allTextBoxes = new List<TextBox>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _themePreference = ThemeManager.LoadThemePreference();
            _isDarkMode = ThemeManager.ResolveEffectiveDarkMode(_themePreference);

            this.Text = Strings.AppName;
            this.Size = new Size(1380, 820);
            this.MinimumSize = new Size(880, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            try
            {
                this.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            }
            catch
            {
                this.Font = SystemFonts.MessageBoxFont;
            }
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;
            this.AllowDrop = true;
            this.Icon = SystemIcons.Application;

            this.HandleCreated += (s, e) => {
                ThemeManager.SetWindowDarkMode(this.Handle, _isDarkMode);
            };

            // 1. MenuStrip (strictly NO top toolbar buttons!)
            BuildMenuBar();

            // 2. Layout SplitContainers
            BuildLayoutPanels();

            // 3. Center Area (Directories Tree & Files View)
            BuildCenterViews();

            // 4. Left Sidebar (Torrent Info)
            BuildLeftSidebar();

            // 5. Right Sidebar (File Details)
            BuildRightSidebar();

            // 6. Apply initial theme
            ApplyTheme(_isDarkMode);

            // System theme changes (auto-detect in real-time)
            SystemEvents.UserPreferenceChanged += MainForm_UserPreferenceChanged;
            this.FormClosing += (s, e) => {
                SystemEvents.UserPreferenceChanged -= MainForm_UserPreferenceChanged;
            };

            // Events
            this.Shown += MainForm_Shown;
            this.Resize += MainForm_Resize;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
        }

        #region UI Construction

        private void BuildMenuBar()
        {
            _menuStrip = new MenuStrip();
            _menuStrip.Dock = DockStyle.Top;
            _menuStrip.RenderMode = ToolStripRenderMode.System;
            _menuStrip.BackColor = SystemColors.Control;
            _menuStrip.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            // --- FILE MENU ---
            ToolStripMenuItem menuFile = new ToolStripMenuItem(Strings.MenuFile);

            ToolStripMenuItem itemOpen = new ToolStripMenuItem(Strings.MenuOpenTorrent, null, (s, e) => ActionOpenTorrent(), Keys.Control | Keys.O);
            ToolStripMenuItem itemClose = new ToolStripMenuItem(Strings.MenuCloseTorrent, null, (s, e) => ActionCloseTorrent(), Keys.Control | Keys.W);
            ToolStripSeparator sepFile1 = new ToolStripSeparator();
            ToolStripMenuItem itemCopyMagnet = new ToolStripMenuItem(Strings.MenuCopyMagnet, null, (s, e) => ActionCopyMagnet(), Keys.Control | Keys.M);
            ToolStripMenuItem itemCopyHash = new ToolStripMenuItem(Strings.MenuCopyInfoHash, null, (s, e) => ActionCopyInfoHash(), Keys.Control | Keys.H);
            ToolStripSeparator sepFile2 = new ToolStripSeparator();
            ToolStripMenuItem itemExit = new ToolStripMenuItem(Strings.MenuExit, null, (s, e) => this.Close(), Keys.Alt | Keys.F4);

            menuFile.DropDownItems.AddRange(new ToolStripItem[] {
                itemOpen, itemClose, sepFile1, itemCopyMagnet, itemCopyHash, sepFile2, itemExit
            });

            // --- EDIT MENU ---
            ToolStripMenuItem menuEdit = new ToolStripMenuItem(Strings.MenuEdit);

            ToolStripMenuItem itemCopyName = new ToolStripMenuItem(Strings.MenuCopyItemName, null, (s, e) => ActionCopySelectedName(), Keys.Control | Keys.C);
            ToolStripMenuItem itemCopySize = new ToolStripMenuItem(Strings.MenuCopyFileSize, null, (s, e) => ActionCopySelectedSize(), Keys.Control | Keys.Shift | Keys.S);
            ToolStripSeparator sepEdit1 = new ToolStripSeparator();
            ToolStripMenuItem itemSelectAll = new ToolStripMenuItem(Strings.MenuSelectAll, null, (s, e) => ActionSelectAll(), Keys.Control | Keys.A);

            menuEdit.DropDownItems.AddRange(new ToolStripItem[] {
                itemCopyName, itemCopySize, sepEdit1, itemSelectAll
            });

            // --- VIEW MENU ---
            ToolStripMenuItem menuView = new ToolStripMenuItem(Strings.MenuView);

            ToolStripMenuItem itemGoUp = new ToolStripMenuItem(Strings.MenuGoToParent, null, (s, e) => ActionNavigateUp(), Keys.Alt | Keys.Up);
            ToolStripMenuItem itemRefresh = new ToolStripMenuItem(Strings.MenuRefresh, null, (s, e) => RefreshCurrentDirectory(), Keys.F5);
            ToolStripSeparator sepView1 = new ToolStripSeparator();
            ToolStripMenuItem itemExpandAll = new ToolStripMenuItem(Strings.MenuExpandAll, null, (s, e) => _treeDirectories.ExpandAll(), Keys.Control | Keys.E);
            ToolStripMenuItem itemCollapseAll = new ToolStripMenuItem(Strings.MenuCollapseAll, null, (s, e) => _treeDirectories.CollapseAll(), Keys.Control | Keys.K);
            ToolStripSeparator sepView2 = new ToolStripSeparator();

            _menuShowLeftSidebar = new ToolStripMenuItem(Strings.MenuShowLeftSidebar, null, (s, e) => ToggleLeftSidebar(), Keys.Control | Keys.D1);
            _menuShowLeftSidebar.Checked = true;
            _menuShowLeftSidebar.CheckOnClick = true;

            _menuShowRightSidebar = new ToolStripMenuItem(Strings.MenuShowRightSidebar, null, (s, e) => ToggleRightSidebar(), Keys.Control | Keys.D2);
            _menuShowRightSidebar.Checked = true;
            _menuShowRightSidebar.CheckOnClick = true;

            ToolStripMenuItem itemFullFilesView = new ToolStripMenuItem(Strings.MenuMaximizeFilesView, null, (s, e) => ToggleMaximizedFilesView(), Keys.Control | Keys.D3);

            _menuThemeAuto = new ToolStripMenuItem(Strings.MenuAutoDetectTheme, null, (s, e) => SetThemePreference(ThemePreference.Auto));
            _menuDarkMode = new ToolStripMenuItem(Strings.MenuDarkMode, null, (s, e) => ToggleDarkMode(), Keys.Control | Keys.D);
            UpdateThemeMenuChecks();

            ToolStripSeparator sepView3 = new ToolStripSeparator();
            ToolStripMenuItem itemResetLayout = new ToolStripMenuItem(Strings.MenuResetLayout, null, (s, e) => ResetLayout(), Keys.Control | Keys.R);

            menuView.DropDownItems.AddRange(new ToolStripItem[] {
                itemGoUp, itemRefresh, sepView1, itemExpandAll, itemCollapseAll, sepView2,
                _menuShowLeftSidebar, _menuShowRightSidebar, itemFullFilesView, _menuThemeAuto, _menuDarkMode, sepView3, itemResetLayout
            });

            // --- HELP MENU ---
            ToolStripMenuItem menuHelp = new ToolStripMenuItem(Strings.MenuHelp);
            ToolStripMenuItem itemAbout = new ToolStripMenuItem(Strings.AboutMenuItem, null, (s, e) => ActionShowAbout(), Keys.F1);
            menuHelp.DropDownItems.Add(itemAbout);

            _menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuEdit, menuView, menuHelp });
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);
        }

        private void BuildLayoutPanels()
        {
            _clientContainer = new Panel();
            _clientContainer.Dock = DockStyle.Fill;
            _clientContainer.BackColor = SystemColors.Control;
            this.Controls.Add(_clientContainer);
            _clientContainer.BringToFront();

            // Outer SplitContainer: Left Sidebar | Remainder
            // FixedPanel = None and PanelMinSizes = 0 prevents InvalidOperationException
            _splitMain = new SplitContainer();
            _splitMain.Dock = DockStyle.Fill;
            _splitMain.Orientation = Orientation.Vertical;
            _splitMain.FixedPanel = FixedPanel.None;
            _splitMain.Panel1MinSize = 0;
            _splitMain.Panel2MinSize = 0;
            _splitMain.SplitterWidth = 4;
            _splitMain.BackColor = SystemColors.Control;

            // Secondary SplitContainer: Center Area | Right Sidebar
            _splitCenterAndRight = new SplitContainer();
            _splitCenterAndRight.Dock = DockStyle.Fill;
            _splitCenterAndRight.Orientation = Orientation.Vertical;
            _splitCenterAndRight.FixedPanel = FixedPanel.None;
            _splitCenterAndRight.Panel1MinSize = 0;
            _splitCenterAndRight.Panel2MinSize = 0;
            _splitCenterAndRight.SplitterWidth = 4;
            _splitCenterAndRight.BackColor = SystemColors.Control;

            // Center Area SplitContainer: TreeView (20%) | FileView (80%)
            _splitTreeAndFiles = new SplitContainer();
            _splitTreeAndFiles.Dock = DockStyle.Fill;
            _splitTreeAndFiles.Orientation = Orientation.Vertical;
            _splitTreeAndFiles.FixedPanel = FixedPanel.None;
            _splitTreeAndFiles.Panel1MinSize = 0;
            _splitTreeAndFiles.Panel2MinSize = 0;
            _splitTreeAndFiles.SplitterWidth = 4;
            _splitTreeAndFiles.BackColor = SystemColors.Control;
            _splitTreeAndFiles.SplitterMoved += (s, e) => {
                if (!_isAdjustingSplitters)
                {
                    _userMovedCenterSplitter = true;
                }
            };

            _splitCenterAndRight.Panel1.Controls.Add(_splitTreeAndFiles);
            _splitMain.Panel2.Controls.Add(_splitCenterAndRight);
            _clientContainer.Controls.Add(_splitMain);
        }

        private void BuildCenterViews()
        {
            // 1. TreeView (Directories only, Windows Classic dotted lines with [+] / [-])
            _treeDirectories = new TreeView();
            _treeDirectories.Dock = DockStyle.Fill;
            _treeDirectories.BorderStyle = BorderStyle.Fixed3D;
            _treeDirectories.BackColor = SystemColors.Window;
            _treeDirectories.ForeColor = SystemColors.WindowText;
            _treeDirectories.ShowLines = true;
            _treeDirectories.ShowPlusMinus = true;
            _treeDirectories.ShowRootLines = true;
            _treeDirectories.HideSelection = false;
            _treeDirectories.Scrollable = true;
            _treeDirectories.ItemHeight = 18;
            _treeDirectories.Indent = 19;
            _treeDirectories.ImageList = ShellIconHelper.SmallImageList;
            _treeDirectories.AfterSelect += TreeDirectories_AfterSelect;
            _treeDirectories.HandleCreated += (s, e) => {
                if (_isDarkMode) ShellIconHelper.ApplyDarkTheme(_treeDirectories);
                else ShellIconHelper.ApplyClassicTheme(_treeDirectories);
            };
            _treeDirectories.AllowDrop = true;
            _treeDirectories.DragEnter += MainForm_DragEnter;
            _treeDirectories.DragDrop += MainForm_DragDrop;

            Panel pnlTreeHeader = CreateSectionHeaderPanel(Strings.SectionDirectories);

            // Docking order: Add Fill control first, Top control second
            _splitTreeAndFiles.Panel1.Controls.Add(_treeDirectories);
            _splitTreeAndFiles.Panel1.Controls.Add(pnlTreeHeader);

            // 2. FileView Area (Top Address Bar + ListView)
            _pnlBreadcrumb = new Panel();
            _pnlBreadcrumb.Dock = DockStyle.Top;
            _pnlBreadcrumb.Height = 26;
            _pnlBreadcrumb.BackColor = SystemColors.Control;
            _pnlBreadcrumb.Padding = new Padding(4, 2, 4, 2);

            _lblAddressTitle = new Label();
            _lblAddressTitle.Text = Strings.AddressTitle;
            _lblAddressTitle.AutoSize = true;
            _lblAddressTitle.Location = new Point(4, 5);
            _lblAddressTitle.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            _lblAddressTitle.ForeColor = SystemColors.ControlText;

            _pnlAddressBox = new Panel();
            _pnlAddressBox.BorderStyle = BorderStyle.Fixed3D;
            _pnlAddressBox.BackColor = SystemColors.Window;
            _pnlAddressBox.Location = new Point(56, 2);
            _pnlAddressBox.Size = new Size(150, 22);

            _picBreadcrumbIcon = new PictureBox();
            _picBreadcrumbIcon.Size = new Size(16, 16);
            _picBreadcrumbIcon.Location = new Point(2, 1);
            _picBreadcrumbIcon.Image = ShellIconHelper.SmallImageList.Images[ShellIconHelper.FolderIconIndex];
            _picBreadcrumbIcon.SizeMode = PictureBoxSizeMode.CenterImage;

            _lblBreadcrumb = new Label();
            _lblBreadcrumb.Location = new Point(22, 2);
            _lblBreadcrumb.AutoSize = true;
            _lblBreadcrumb.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            _lblBreadcrumb.Text = Strings.RootPathBackslash;
            _lblBreadcrumb.ForeColor = SystemColors.WindowText;
            _lblBreadcrumb.BackColor = SystemColors.Window;

            _pnlAddressBox.Controls.Add(_picBreadcrumbIcon);
            _pnlAddressBox.Controls.Add(_lblBreadcrumb);

            _lblFolderSummary = new Label();
            _lblFolderSummary.AutoSize = true;
            _lblFolderSummary.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            _lblFolderSummary.ForeColor = SystemColors.ControlText;
            _lblFolderSummary.Text = Strings.FolderSummaryEmpty;
            _lblFolderSummary.Location = new Point(_pnlBreadcrumb.ClientSize.Width - 75, 5);
            _lblFolderSummary.TextChanged += (s, e) => UpdateAddressBarLayout();

            _pnlBreadcrumb.Controls.Add(_pnlAddressBox);
            _pnlBreadcrumb.Controls.Add(_lblAddressTitle);
            _pnlBreadcrumb.Controls.Add(_lblFolderSummary);

            _pnlBreadcrumb.Resize += (s, e) => UpdateAddressBarLayout();

            // ListView with direct items (Windows Classic 3D headers)
            _lvFiles = new ListView();
            _lvFiles.Dock = DockStyle.Fill;
            _lvFiles.BorderStyle = BorderStyle.Fixed3D;
            _lvFiles.BackColor = SystemColors.Window;
            _lvFiles.ForeColor = SystemColors.WindowText;
            _lvFiles.View = View.Details;
            _lvFiles.FullRowSelect = true;
            _lvFiles.GridLines = false;
            _lvFiles.MultiSelect = true;
            _lvFiles.HideSelection = false;
            _lvFiles.SmallImageList = ShellIconHelper.SmallImageList;
            _lvFiles.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            // Columns
            _lvFiles.Columns.Add(Strings.ColName, 320);
            _lvFiles.Columns.Add(Strings.ColSize, 95, HorizontalAlignment.Right);
            _lvFiles.Columns.Add(Strings.ColType, 130);
            _lvFiles.Columns.Add(Strings.ColPieces, 130);

            _lvFiles.SelectedIndexChanged += LvFiles_SelectedIndexChanged;
            _lvFiles.DoubleClick += LvFiles_DoubleClick;
            _lvFiles.KeyDown += LvFiles_KeyDown;
            _lvFiles.ColumnClick += LvFiles_ColumnClick;
            _lvFiles.Resize += (s, e) => AdjustListViewColumns();
            _lvFiles.HandleCreated += (s, e) => {
                AttachHeaderHook();
                if (_isDarkMode) ShellIconHelper.ApplyDarkTheme(_lvFiles);
                else ShellIconHelper.ApplyClassicTheme(_lvFiles);
            };
            _lvFiles.HandleDestroyed += (s, e) => {
                DetachHeaderHook();
            };
            _lvFiles.DrawColumnHeader += LvFiles_DrawColumnHeader;
            _lvFiles.DrawItem += (s, e) => { e.DrawDefault = true; };
            _lvFiles.DrawSubItem += (s, e) => { e.DrawDefault = true; };
            _lvFiles.AllowDrop = true;
            _lvFiles.DragEnter += MainForm_DragEnter;
            _lvFiles.DragDrop += MainForm_DragDrop;

            // Context Menu for FileView
            ContextMenuStrip ctxFiles = new ContextMenuStrip();
            ctxFiles.RenderMode = ToolStripRenderMode.System;
            ctxFiles.Items.Add(Strings.CtxCopyItemName, null, (s, e) => ActionCopySelectedName());
            ctxFiles.Items.Add(Strings.CtxCopyFileSize, null, (s, e) => ActionCopySelectedSize());
            ctxFiles.Items.Add(new ToolStripSeparator());
            ctxFiles.Items.Add(Strings.CtxGoToParent, null, (s, e) => ActionNavigateUp());
            _lvFiles.ContextMenuStrip = ctxFiles;

            // Docking order: Add ListView (Fill) first, Address Bar (Top) second
            _splitTreeAndFiles.Panel2.Controls.Add(_lvFiles);
            _splitTreeAndFiles.Panel2.Controls.Add(_pnlBreadcrumb);
        }

        private void BuildLeftSidebar()
        {
            Panel pnlHeader = CreateSectionHeaderPanel(Strings.SectionTorrentInfo);

            _pnlLeftContent = new Panel();
            _pnlLeftContent.Dock = DockStyle.Fill;
            _pnlLeftContent.AutoScroll = true;
            _pnlLeftContent.Padding = new Padding(6, 4, 6, 6);
            _pnlLeftContent.BackColor = SystemColors.Control;
            _pnlLeftContent.AllowDrop = true;
            _pnlLeftContent.DragEnter += MainForm_DragEnter;
            _pnlLeftContent.DragDrop += MainForm_DragDrop;

            // GROUP 3: TRACKERS (Added bottom-to-top)
            GroupBox grpTrackers = CreateClassicGroupBox(Strings.GroupTrackers);
            _lstTrackers = AddListBoxField(grpTrackers, Strings.LabelAllTrackers, 75);
            _txtPrimaryTracker = AddRowField(grpTrackers, Strings.LabelPrimaryTracker, 21, false);

            // GROUP 2: METADATA
            GroupBox grpMeta = CreateClassicGroupBox(Strings.GroupMetadata);
            _txtComment = AddRowField(grpMeta, Strings.LabelComment, 48, true);
            _lblCreationDate = AddRowProperty(grpMeta, Strings.LabelDateCreated);
            _lblCreatedBy = AddRowProperty(grpMeta, Strings.LabelCreatedBy);

            // GROUP 1: GENERAL INFORMATION
            GroupBox grpGen = CreateClassicGroupBox(Strings.GroupGeneralInfo);
            _lblPrivacy = AddRowProperty(grpGen, Strings.LabelPrivacy);
            _lblPiecesCount = AddRowProperty(grpGen, Strings.LabelPiecesCount);
            _lblPieceSize = AddRowProperty(grpGen, Strings.LabelPieceLength);
            _lblFilesCount = AddRowProperty(grpGen, Strings.LabelContentCount);
            _lblTotalSize = AddRowProperty(grpGen, Strings.LabelTotalSize);
            Button btnCopyHash;
            _txtInfoHash = AddHashField(grpGen, Strings.LabelInfoHash, out btnCopyHash);
            _txtTorrentName = AddRowField(grpGen, Strings.LabelTorrentName, 38, true);

            // Add GroupBoxes in bottom-to-top order to _pnlLeftContent
            _pnlLeftContent.Controls.Add(grpTrackers);
            _pnlLeftContent.Controls.Add(CreateSpacer(6));
            _pnlLeftContent.Controls.Add(grpMeta);
            _pnlLeftContent.Controls.Add(CreateSpacer(6));
            _pnlLeftContent.Controls.Add(grpGen);

            // Docking order: Add Content (Fill) first, Header (Top) second
            _splitMain.Panel1.Controls.Add(_pnlLeftContent);
            _splitMain.Panel1.Controls.Add(pnlHeader);
        }

        private void BuildRightSidebar()
        {
            Panel pnlHeader = CreateSectionHeaderPanel(Strings.SectionSelectedItemDetails);

            _pnlRightContent = new Panel();
            _pnlRightContent.Dock = DockStyle.Fill;
            _pnlRightContent.AutoScroll = true;
            _pnlRightContent.Padding = new Padding(6, 4, 6, 6);
            _pnlRightContent.BackColor = SystemColors.Control;
            _pnlRightContent.AllowDrop = true;
            _pnlRightContent.DragEnter += MainForm_DragEnter;
            _pnlRightContent.DragDrop += MainForm_DragDrop;

            // Placeholder message when nothing is loaded
            _lblRightPlaceholder = new Label();
            _lblRightPlaceholder.Text = Strings.SelectionPlaceholder;
            _lblRightPlaceholder.ForeColor = SystemColors.GrayText;
            _lblRightPlaceholder.Font = new Font("Tahoma", 8.25f, FontStyle.Italic);
            _lblRightPlaceholder.Dock = DockStyle.Top;
            _lblRightPlaceholder.Padding = new Padding(4, 8, 4, 4);
            _lblRightPlaceholder.AutoSize = true;

            // SELECTION HEADER GROUPBOX
            _grpSelection = CreateClassicGroupBox(Strings.GroupSelection);
            _grpSelection.Height = 65;
            _grpSelection.AutoSize = false;

            _picItemLargeIcon = new PictureBox();
            _picItemLargeIcon.Size = new Size(32, 32);
            _picItemLargeIcon.Location = new Point(12, 20);
            _picItemLargeIcon.SizeMode = PictureBoxSizeMode.Normal;

            _lblItemHeaderName = new Label();
            _lblItemHeaderName.Location = new Point(52, 18);
            _lblItemHeaderName.Size = new Size(180, 16);
            _lblItemHeaderName.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            _lblItemHeaderName.ForeColor = SystemColors.ControlText;
            _lblItemHeaderName.BackColor = Color.Transparent;
            _lblItemHeaderName.AutoEllipsis = true;
            _lblItemHeaderName.Text = Strings.SelectionNoSelection;

            _lblItemHeaderType = new Label();
            _lblItemHeaderType.Location = new Point(52, 38);
            _lblItemHeaderType.Size = new Size(180, 16);
            _lblItemHeaderType.ForeColor = SystemColors.GrayText;
            _lblItemHeaderType.BackColor = Color.Transparent;
            _lblItemHeaderType.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            _lblItemHeaderType.AutoEllipsis = true;
            _lblItemHeaderType.Text = string.Empty;

            _grpSelection.Controls.Add(_picItemLargeIcon);
            _grpSelection.Controls.Add(_lblItemHeaderName);
            _grpSelection.Controls.Add(_lblItemHeaderType);

            // PROPERTIES GROUPBOX
            GroupBox grpProps = CreateClassicGroupBox(Strings.GroupProperties);

            // Add rows in bottom-to-top order
            _lblItemShare = AddRowProperty(grpProps, Strings.LabelTorrentShare);
            _lblItemPieceRange = AddRowProperty(grpProps, Strings.LabelPieceRange);
            _lblItemOffset = AddRowProperty(grpProps, Strings.LabelFileOffset);
            _lblItemExt = AddRowProperty(grpProps, Strings.LabelExtension);
            _lblItemType = AddRowProperty(grpProps, Strings.LabelFileType);
            _lblItemSize = AddRowProperty(grpProps, Strings.LabelSize);

            // Add to Right Sidebar in bottom-to-top order
            _pnlRightContent.Controls.Add(grpProps);
            _pnlRightContent.Controls.Add(CreateSpacer(6));
            _pnlRightContent.Controls.Add(_grpSelection);
            _pnlRightContent.Controls.Add(_lblRightPlaceholder);

            // Docking order: Add Content (Fill) first, Header (Top) second
            _splitCenterAndRight.Panel2.Controls.Add(_pnlRightContent);
            _splitCenterAndRight.Panel2.Controls.Add(pnlHeader);
        }

        private void UpdateAddressBarLayout()
        {
            if (_pnlBreadcrumb != null && _pnlAddressBox != null && _lblFolderSummary != null)
            {
                int summaryW = _lblFolderSummary.PreferredWidth;
                int targetLeft = Math.Max(120, _pnlBreadcrumb.ClientSize.Width - summaryW - 8);
                _lblFolderSummary.Left = targetLeft;
                _pnlAddressBox.Width = Math.Max(50, targetLeft - _pnlAddressBox.Left - 8);
            }
        }

        private Panel CreateSectionHeaderPanel(string title)
        {
            Panel pnl = new Panel();
            pnl.Height = 26;
            pnl.BackColor = SystemColors.Control;
            pnl.Padding = new Padding(6, 4, 6, 2);
            pnl.Dock = DockStyle.Top;

            Label lbl = new Label();
            lbl.Text = title;
            lbl.Font = new Font("Tahoma", 8.25f, FontStyle.Bold);
            lbl.ForeColor = SystemColors.ControlText;
            lbl.Dock = DockStyle.Fill;
            lbl.TextAlign = ContentAlignment.MiddleLeft;

            pnl.Controls.Add(lbl);
            pnl.Paint += (s, e) => {
                if (_isDarkMode)
                {
                    using (Pen p = new Pen(Color.FromArgb(52, 52, 52)))
                    {
                        e.Graphics.DrawLine(p, 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
                    }
                }
                else
                {
                    ControlPaint.DrawBorder3D(e.Graphics, 0, pnl.Height - 2, pnl.Width, 2, Border3DStyle.Etched, Border3DSide.Bottom);
                }
            };

            _sectionHeaderPanels.Add(pnl);
            _sectionHeaderLabels.Add(lbl);
            return pnl;
        }

        private GroupBox CreateClassicGroupBox(string groupTitle)
        {
            GroupBox grp = new GroupBox();
            grp.Text = groupTitle;
            grp.Dock = DockStyle.Top;
            grp.AutoSize = true;
            grp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            grp.FlatStyle = FlatStyle.Standard;
            grp.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            grp.ForeColor = SystemColors.ControlText;
            grp.Padding = new Padding(8, 8, 8, 6);
            _allGroupBoxes.Add(grp);
            return grp;
        }

        private Panel CreateSpacer(int height)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = height;
            p.BackColor = Color.Transparent;
            return p;
        }

        private Label AddRowProperty(GroupBox parent, string title, string defaultValue = Strings.DefaultDash)
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 20;
            row.Padding = new Padding(0, 2, 0, 2);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Location = new Point(2, 2);
            lblTitle.Size = new Size(76, 16);
            lblTitle.ForeColor = SystemColors.ControlDarkDark;
            lblTitle.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            Label lblVal = new Label();
            lblVal.Text = defaultValue;
            lblVal.Location = new Point(80, 2);
            lblVal.Size = new Size(Math.Max(50, parent.ClientSize.Width - 84), 16);
            lblVal.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            lblVal.ForeColor = SystemColors.ControlText;
            lblVal.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            lblVal.AutoEllipsis = true;

            row.Controls.Add(lblVal);
            row.Controls.Add(lblTitle);
            parent.Controls.Add(row);

            _propertyTitleLabels.Add(lblTitle);
            _propertyValueLabels.Add(lblVal);
            return lblVal;
        }

        private TextBox AddRowField(GroupBox parent, string title, int height, bool multiline)
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 16 + height + 4;
            row.Padding = new Padding(0, 1, 0, 3);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 16;
            lblTitle.ForeColor = SystemColors.ControlDarkDark;
            lblTitle.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            TextBox txt = new TextBox();
            txt.Dock = DockStyle.Fill;
            txt.ReadOnly = true;
            txt.BorderStyle = BorderStyle.Fixed3D;
            txt.BackColor = SystemColors.Window;
            txt.ForeColor = SystemColors.WindowText;
            txt.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            if (multiline)
            {
                txt.Multiline = true;
                txt.ScrollBars = ScrollBars.Vertical;
            }

            row.Controls.Add(txt);
            row.Controls.Add(lblTitle);
            parent.Controls.Add(row);

            _propertyTitleLabels.Add(lblTitle);
            _allTextBoxes.Add(txt);
            return txt;
        }

        private TextBox AddHashField(GroupBox parent, string title, out Button copyButton)
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 16 + 23 + 4;
            row.Padding = new Padding(0, 1, 0, 3);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 16;
            lblTitle.ForeColor = SystemColors.ControlDarkDark;
            lblTitle.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            Panel sub = new Panel();
            sub.Dock = DockStyle.Fill;

            copyButton = new Button();
            copyButton.Text = Strings.ButtonCopy;
            copyButton.Width = 50;
            copyButton.Dock = DockStyle.Right;
            copyButton.FlatStyle = FlatStyle.Standard;
            copyButton.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            copyButton.Click += (s, e) => ActionCopyInfoHash();

            TextBox txt = new TextBox();
            txt.Dock = DockStyle.Fill;
            txt.ReadOnly = true;
            txt.BorderStyle = BorderStyle.Fixed3D;
            txt.BackColor = SystemColors.Window;
            txt.ForeColor = SystemColors.WindowText;
            txt.Font = new Font("Consolas", 8.25f, FontStyle.Regular);

            sub.Controls.Add(txt);
            sub.Controls.Add(copyButton);

            row.Controls.Add(sub);
            row.Controls.Add(lblTitle);
            parent.Controls.Add(row);

            _btnCopyHash = copyButton;
            _propertyTitleLabels.Add(lblTitle);
            _allTextBoxes.Add(txt);
            return txt;
        }

        private ListBox AddListBoxField(GroupBox parent, string title, int height)
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 16 + height + 4;
            row.Padding = new Padding(0, 1, 0, 3);

            Label lblTitle = new Label();
            lblTitle.Text = title;
            lblTitle.Dock = DockStyle.Top;
            lblTitle.Height = 16;
            lblTitle.ForeColor = SystemColors.ControlDarkDark;
            lblTitle.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);

            ListBox lst = new ListBox();
            lst.Dock = DockStyle.Fill;
            lst.BorderStyle = BorderStyle.Fixed3D;
            lst.BackColor = SystemColors.Window;
            lst.ForeColor = SystemColors.WindowText;
            lst.Font = new Font("Tahoma", 8.25f, FontStyle.Regular);
            lst.HorizontalScrollbar = true;

            row.Controls.Add(lst);
            row.Controls.Add(lblTitle);
            parent.Controls.Add(row);

            _propertyTitleLabels.Add(lblTitle);
            return lst;
        }

        #endregion

        #region Form Lifecycle & Resizing

        private void MainForm_Shown(object sender, EventArgs e)
        {
            ResetLayout();
            AdjustListViewColumns();
            ThemeManager.SetWindowDarkMode(this.Handle, _isDarkMode);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized) return;
            if (_splitTreeAndFiles == null || _splitTreeAndFiles.Width <= 20) return;

            // Maintain the 20% TreeView / 80% FileView ratio if user hasn't moved the splitter
            if (!_userMovedCenterSplitter)
            {
                _isAdjustingSplitters = true;
                try
                {
                    int desiredTreeWidth = Math.Max(100, (int)(_splitTreeAndFiles.Width * 0.20));
                    SafeSetSplitterDistance(_splitTreeAndFiles, desiredTreeWidth);
                }
                finally
                {
                    _isAdjustingSplitters = false;
                }
            }

            // Keep right sidebar header label widths responsive and strictly inside the groupbox
            if (_grpSelection != null && _lblItemHeaderName != null && _lblItemHeaderType != null)
            {
                int maxLabelW = Math.Max(50, _grpSelection.ClientSize.Width - 52 - 12);
                _lblItemHeaderName.Width = maxLabelW;
                _lblItemHeaderType.Width = maxLabelW;
            }

            AdjustListViewColumns();
        }

        private void AdjustListViewColumns()
        {
            if (_lvFiles == null || _lvFiles.Columns.Count < 4) return;
            int clientWidth = _lvFiles.ClientSize.Width;
            if (clientWidth <= 100) return;

            // Fixed widths for Size, Type, Pieces
            int fixedWidths = _lvFiles.Columns[1].Width + _lvFiles.Columns[2].Width + _lvFiles.Columns[3].Width;
            int scrollPad = (_lvFiles.Items.Count * 20 > _lvFiles.ClientSize.Height)
                ? (SystemInformation.VerticalScrollBarWidth + 2)
                : 2;
            int remaining = clientWidth - fixedWidths - scrollPad;

            // Name column automatically absorbs all remaining width so names are never truncated!
            if (remaining > 220)
            {
                _lvFiles.Columns[0].Width = remaining;
            }
            else
            {
                _lvFiles.Columns[0].Width = 240;
            }
        }

        private void ResetLayout()
        {
            if (this.WindowState == FormWindowState.Minimized) return;

            _isAdjustingSplitters = true;
            try
            {
                // Left Sidebar: 300px spacious width
                SafeSetSplitterDistance(_splitMain, 300);

                // Right Sidebar: 300px spacious width from the right edge
                if (_splitCenterAndRight.Width > 340)
                {
                    SafeSetSplitterDistance(_splitCenterAndRight, _splitCenterAndRight.Width - 300);
                }

                // Center: 20% TreeView, 80% FileView
                if (_splitTreeAndFiles.Width > 50)
                {
                    int desiredTreeWidth = Math.Max(100, (int)(_splitTreeAndFiles.Width * 0.20));
                    SafeSetSplitterDistance(_splitTreeAndFiles, desiredTreeWidth);
                }

                _userMovedCenterSplitter = false;
            }
            finally
            {
                _isAdjustingSplitters = false;
            }

            _menuShowLeftSidebar.Checked = true;
            _menuShowRightSidebar.Checked = true;
            _splitMain.Panel1Collapsed = false;
            _splitCenterAndRight.Panel2Collapsed = false;

            AdjustListViewColumns();
        }

        // Bulletproof helper to prevent InvalidOperationException on any resize or DPI scale
        private void SafeSetSplitterDistance(SplitContainer sc, int distance)
        {
            if (sc == null || sc.IsDisposed || !sc.IsHandleCreated) return;
            if (this.WindowState == FormWindowState.Minimized) return;
            if (sc.Width <= 40) return;

            int minAllowed = 20;
            int maxAllowed = sc.Width - 20 - sc.SplitterWidth;

            if (maxAllowed > minAllowed)
            {
                int clamped = Math.Max(minAllowed, Math.Min(distance, maxAllowed));
                try
                {
                    sc.SplitterDistance = clamped;
                }
                catch
                {
                    // Ignore any transient layout exceptions
                }
            }
        }

        private void ToggleLeftSidebar()
        {
            _splitMain.Panel1Collapsed = !_menuShowLeftSidebar.Checked;
            AdjustListViewColumns();
        }

        private void ToggleRightSidebar()
        {
            _splitCenterAndRight.Panel2Collapsed = !_menuShowRightSidebar.Checked;
            AdjustListViewColumns();
        }

        private void ToggleMaximizedFilesView()
        {
            bool shouldHide = !_splitMain.Panel1Collapsed || !_splitCenterAndRight.Panel2Collapsed;
            _splitMain.Panel1Collapsed = shouldHide;
            _splitCenterAndRight.Panel2Collapsed = shouldHide;
            _menuShowLeftSidebar.Checked = !shouldHide;
            _menuShowRightSidebar.Checked = !shouldHide;
            AdjustListViewColumns();
        }

        #endregion

        #region Torrent Loading & Display

        public void LoadTorrent(string filePath)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                this.Update();

                TorrentMetadata meta = BencodeParser.LoadFromFile(filePath);
                ApplyTorrentMetadata(meta);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(Strings.TorrentParseErrorMessage, ex.Message), Strings.TorrentParseErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void ApplyTorrentMetadata(TorrentMetadata meta)
        {
            _metadata = meta;
            this.Text = string.Format(Strings.AppTitleLoaded, meta.Name);

            // 1. Resolve native file types and icons for all files once
            for (int i = 0; i < _metadata.AllFiles.Count; i++)
            {
                TorrentFile file = _metadata.AllFiles[i];
                file.TypeDescription = ShellIconHelper.GetFileTypeName(file.Extension);
                ShellIconHelper.GetSmallIconIndexForExtension(file.Extension);
            }

            // 2. Populate Left Sidebar (Torrent Info)
            _txtTorrentName.Text = meta.Name;
            _txtInfoHash.Text = meta.InfoHash;
            _lblTotalSize.Text = string.Format(Strings.FormatSizeWithBytes, meta.FormattedTotalSize, meta.TotalSize);
            _lblFilesCount.Text = string.Format(Strings.FormatContentCount, meta.TotalFilesCount, meta.TotalFoldersCount);
            _lblPieceSize.Text = meta.FormattedPieceLength;
            _lblPiecesCount.Text = string.Format(Strings.FormatPiecesCount, meta.PieceCount);
            _lblPrivacy.Text = meta.IsPrivate ? Strings.ValuePrivateTracker : Strings.ValuePublicTorrent;

            _lblCreatedBy.Text = string.IsNullOrEmpty(meta.CreatedBy) ? Strings.ValueNotSpecified : meta.CreatedBy;
            _lblCreationDate.Text = meta.CreationDate.HasValue ? meta.CreationDate.Value.ToString(Strings.FormatDateTime) : Strings.ValueNotSpecified;
            _txtComment.Text = string.IsNullOrEmpty(meta.Comment) ? Strings.ValueNone : meta.Comment;

            _txtPrimaryTracker.Text = string.IsNullOrEmpty(meta.PrimaryTracker) ? Strings.ValueNone : meta.PrimaryTracker;

            _lstTrackers.Items.Clear();
            for (int i = 0; i < meta.Trackers.Count; i++)
            {
                _lstTrackers.Items.Add(meta.Trackers[i]);
            }
            if (_lstTrackers.Items.Count == 0)
            {
                _lstTrackers.Items.Add(Strings.ValueNoTrackers);
            }

            // 3. Build Directory Tree (Directories ONLY!)
            BuildDirectoryTree(meta.RootDirectory);

            // 4. Navigate to Root Directory in FileView
            NavigateToDirectory(meta.RootDirectory);

            // 5. Update Right Sidebar with initial root directory summary
            UpdateRightSidebarForDirectory(meta.RootDirectory);
            AdjustListViewColumns();
        }

        private void BuildDirectoryTree(TorrentDirectory rootDir)
        {
            _treeDirectories.BeginUpdate();
            _treeDirectories.Nodes.Clear();
            _dirToNodeMap.Clear();

            TreeNode rootNode = new TreeNode(rootDir.Name);
            rootNode.Tag = rootDir;
            rootNode.ImageIndex = ShellIconHelper.FolderIconIndex;
            rootNode.SelectedImageIndex = ShellIconHelper.FolderIconIndex;

            _dirToNodeMap[rootDir] = rootNode;
            AddDirectoryNodesRecursive(rootNode, rootDir);

            _treeDirectories.Nodes.Add(rootNode);
            rootNode.Expand();
            _treeDirectories.SelectedNode = rootNode;
            _treeDirectories.EndUpdate();
        }

        private void AddDirectoryNodesRecursive(TreeNode parentNode, TorrentDirectory dir)
        {
            for (int i = 0; i < dir.SubDirectories.Count; i++)
            {
                TorrentDirectory sub = dir.SubDirectories[i];
                TreeNode subNode = new TreeNode(sub.Name);
                subNode.Tag = sub;
                subNode.ImageIndex = ShellIconHelper.FolderIconIndex;
                subNode.SelectedImageIndex = ShellIconHelper.FolderIconIndex;

                _dirToNodeMap[sub] = subNode;
                parentNode.Nodes.Add(subNode);

                AddDirectoryNodesRecursive(subNode, sub);
            }
        }

        public void NavigateToDirectory(TorrentDirectory dir)
        {
            if (dir == null) return;
            _currentDirectory = dir;

            // Fetch display items (includes '..' if not root)
            _displayItems = dir.GetDisplayItems();

            // Set small icons for files and folders
            for (int i = 0; i < _displayItems.Count; i++)
            {
                TorrentItem item = _displayItems[i];
                if (item.IsParentFolder)
                {
                    item.ImageIndex = ShellIconHelper.UpFolderIconIndex;
                }
                else if (item.IsFolder)
                {
                    item.ImageIndex = ShellIconHelper.FolderIconIndex;
                }
                else if (item.IsFile)
                {
                    item.ImageIndex = ShellIconHelper.GetSmallIconIndexForExtension(item.File.Extension);
                }
            }

            // Apply current sorting
            SortDisplayItems();

            // Populate ListView items directly into Items collection (100% reliable)
            _lvFiles.BeginUpdate();
            _lvFiles.Items.Clear();

            List<ListViewItem> lvItems = new List<ListViewItem>(_displayItems.Count);
            for (int i = 0; i < _displayItems.Count; i++)
            {
                TorrentItem it = _displayItems[i];
                ListViewItem lvi = new ListViewItem(it.Name);
                lvi.ImageIndex = it.ImageIndex;
                lvi.SubItems.Add(it.SizeDisplay);
                lvi.SubItems.Add(it.TypeDisplay);
                lvi.SubItems.Add(it.PieceRangeDisplay);
                lvi.Tag = it;
                lvItems.Add(lvi);
            }

            _lvFiles.Items.AddRange(lvItems.ToArray());
            _lvFiles.EndUpdate();

            // Update Breadcrumb & Stats
            _lblBreadcrumb.Text = Strings.RootPathBackslash + dir.FullPath;
            int fileCount = dir.Files.Count;
            int subCount = dir.SubDirectories.Count;
            _lblFolderSummary.Text = string.Format(Strings.FolderSummaryFormat, fileCount, subCount, dir.FormattedTotalSize);

            // Synchronize TreeView selection if needed
            TreeNode targetNode;
            if (_dirToNodeMap.TryGetValue(dir, out targetNode))
            {
                if (_treeDirectories.SelectedNode != targetNode)
                {
                    _treeDirectories.SelectedNode = targetNode;
                    targetNode.EnsureVisible();
                }
            }

            AdjustListViewColumns();
        }

        private void RefreshCurrentDirectory()
        {
            if (_currentDirectory != null)
            {
                NavigateToDirectory(_currentDirectory);
            }
        }

        #endregion

        #region ListView Event Handlers

        private void LvFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_lvFiles.SelectedItems.Count == 1)
            {
                TorrentItem item = _lvFiles.SelectedItems[0].Tag as TorrentItem;
                if (item != null)
                {
                    UpdateRightSidebarForItem(item);
                }
            }
            else if (_lvFiles.SelectedItems.Count > 1)
            {
                UpdateRightSidebarMultiSelect();
            }
            else
            {
                if (_currentDirectory != null)
                {
                    UpdateRightSidebarForDirectory(_currentDirectory);
                }
                else
                {
                    UpdateRightSidebarEmpty();
                }
            }
        }

        private void LvFiles_DoubleClick(object sender, EventArgs e)
        {
            ActivateSelectedItem();
        }

        private void LvFiles_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ActivateSelectedItem();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                ActionNavigateUp();
                e.Handled = true;
            }
        }

        private void ActivateSelectedItem()
        {
            if (_lvFiles.SelectedItems.Count == 0) return;
            TorrentItem item = _lvFiles.SelectedItems[0].Tag as TorrentItem;
            if (item == null) return;

            if (item.IsParentFolder)
            {
                ActionNavigateUp();
            }
            else if (item.IsFolder)
            {
                NavigateToDirectory(item.Directory);
            }
        }

        private void LvFiles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (_sortColumn == e.Column)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }

            SortDisplayItems();

            _lvFiles.BeginUpdate();
            _lvFiles.Items.Clear();

            List<ListViewItem> lvItems = new List<ListViewItem>(_displayItems.Count);
            for (int i = 0; i < _displayItems.Count; i++)
            {
                TorrentItem it = _displayItems[i];
                ListViewItem lvi = new ListViewItem(it.Name);
                lvi.ImageIndex = it.ImageIndex;
                lvi.SubItems.Add(it.SizeDisplay);
                lvi.SubItems.Add(it.TypeDisplay);
                lvi.SubItems.Add(it.PieceRangeDisplay);
                lvi.Tag = it;
                lvItems.Add(lvi);
            }

            _lvFiles.Items.AddRange(lvItems.ToArray());
            _lvFiles.EndUpdate();
        }

        private void SortDisplayItems()
        {
            if (_displayItems == null || _displayItems.Count <= 1) return;
            _displayItems.Sort(new TorrentItemComparer(_sortColumn, _sortAscending));
        }

        private void TreeDirectories_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node != null && e.Node.Tag is TorrentDirectory)
            {
                TorrentDirectory dir = (TorrentDirectory)e.Node.Tag;
                if (dir != _currentDirectory)
                {
                    NavigateToDirectory(dir);
                }
            }
        }

        #endregion

        #region Right Sidebar Details Display

        private void UpdateRightSidebarForItem(TorrentItem item)
        {
            if (item == null) return;
            _lblRightPlaceholder.Visible = false;

            if (item.IsParentFolder)
            {
                _picItemLargeIcon.Image = ShellIconHelper.UpFolderLargeImage;
                _lblItemHeaderName.Text = Strings.SelectionParentDirectory;
                _lblItemHeaderType.Text = Strings.SelectionFolderNavigation;
                _lblItemSize.Text = Strings.DefaultDash;
                _lblItemType.Text = Strings.TypeParentFolder;
                _lblItemExt.Text = Strings.DefaultDash;
                _lblItemOffset.Text = Strings.DefaultDash;
                _lblItemPieceRange.Text = Strings.DefaultDash;
                _lblItemShare.Text = Strings.DefaultDash;
            }
            else if (item.IsFolder)
            {
                TorrentDirectory dir = item.Directory;
                _picItemLargeIcon.Image = ShellIconHelper.FolderLargeImage;
                _lblItemHeaderName.Text = dir.Name;
                _lblItemHeaderType.Text = string.Format(Strings.SelectionDirectoryHeader, dir.FormattedTotalSize);
                _lblItemSize.Text = string.Format(Strings.FormatSizeWithBytes, dir.FormattedTotalSize, dir.TotalSize);
                _lblItemType.Text = Strings.TypeFileFolder;
                _lblItemExt.Text = Strings.DefaultDash;
                _lblItemOffset.Text = Strings.DefaultDash;
                _lblItemPieceRange.Text = string.Format(Strings.FormatDirectoryItems, dir.TotalFilesCount, dir.TotalFoldersCount);

                double share = _metadata.TotalSize > 0 ? (dir.TotalSize * 100.0 / _metadata.TotalSize) : 0.0;
                _lblItemShare.Text = string.Format(Strings.FormatTorrentShare, share);
            }
            else if (item.IsFile)
            {
                TorrentFile f = item.File;
                _picItemLargeIcon.Image = ShellIconHelper.GetLargeImageForExtension(f.Extension);
                _lblItemHeaderName.Text = f.Name;
                _lblItemHeaderType.Text = string.Format(Strings.SelectionFileHeader, f.TypeDescription, f.FormattedSize);
                _lblItemSize.Text = string.Format(Strings.FormatSizeWithBytes, f.FormattedSize, f.Length);
                _lblItemType.Text = f.TypeDescription;
                _lblItemExt.Text = string.IsNullOrEmpty(f.Extension) ? Strings.ValueNone : f.Extension;
                _lblItemOffset.Text = string.Format(Strings.FormatSizeWithBytes, f.FormattedOffset, f.Offset);

                _lblItemPieceRange.Text = f.PieceCount > 0
                    ? string.Format(Strings.FormatPiecesSpanned, f.StartPiece, f.EndPiece, f.PieceCount)
                    : Strings.ValueZeroPieces;

                double share = _metadata.TotalSize > 0 ? (f.Length * 100.0 / _metadata.TotalSize) : 0.0;
                _lblItemShare.Text = string.Format(Strings.FormatTorrentShare, share);
            }
        }

        private void UpdateRightSidebarForDirectory(TorrentDirectory dir)
        {
            if (dir == null || _metadata == null)
            {
                UpdateRightSidebarEmpty();
                return;
            }

            _lblRightPlaceholder.Visible = false;
            _picItemLargeIcon.Image = ShellIconHelper.FolderLargeImage;
            _lblItemHeaderName.Text = dir.Name;
            _lblItemHeaderType.Text = string.Format(Strings.SelectionCurrentDirectoryHeader, dir.FormattedTotalSize);
            _lblItemSize.Text = string.Format(Strings.FormatSizeWithBytes, dir.FormattedTotalSize, dir.TotalSize);
            _lblItemType.Text = Strings.TypeFileFolder;
            _lblItemExt.Text = Strings.DefaultDash;
            _lblItemOffset.Text = Strings.DefaultDash;
            _lblItemPieceRange.Text = string.Format(Strings.FormatDirectoryItems, dir.TotalFilesCount, dir.TotalFoldersCount);

            double share = _metadata.TotalSize > 0 ? (dir.TotalSize * 100.0 / _metadata.TotalSize) : 0.0;
            _lblItemShare.Text = string.Format(Strings.FormatTorrentShare, share);
        }

        private void UpdateRightSidebarMultiSelect()
        {
            int count = _lvFiles.SelectedItems.Count;
            long totalSelectedBytes = 0;
            int fileCount = 0;
            int folderCount = 0;

            for (int i = 0; i < count; i++)
            {
                TorrentItem it = _lvFiles.SelectedItems[i].Tag as TorrentItem;
                if (it != null)
                {
                    if (it.IsFile)
                    {
                        totalSelectedBytes += it.File.Length;
                        fileCount++;
                    }
                    else if (it.IsFolder)
                    {
                        totalSelectedBytes += it.Directory.TotalSize;
                        folderCount++;
                    }
                }
            }

            _lblRightPlaceholder.Visible = false;
            _picItemLargeIcon.Image = ShellIconHelper.FolderLargeImage;
            _lblItemHeaderName.Text = string.Format(Strings.SelectionMultipleCount, count);
            _lblItemHeaderType.Text = Strings.SelectionMultiple;
            _lblItemSize.Text = string.Format(Strings.FormatSizeWithBytes, TorrentMetadata.FormatBytes(totalSelectedBytes), totalSelectedBytes);
            _lblItemType.Text = Strings.TypeMultipleItems;
            _lblItemExt.Text = Strings.DefaultDash;
            _lblItemOffset.Text = Strings.DefaultDash;
            _lblItemPieceRange.Text = Strings.DefaultDash;

            double share = (_metadata != null && _metadata.TotalSize > 0) ? (totalSelectedBytes * 100.0 / _metadata.TotalSize) : 0.0;
            _lblItemShare.Text = string.Format(Strings.FormatTorrentShare, share);
        }

        private void UpdateRightSidebarEmpty()
        {
            _lblRightPlaceholder.Visible = true;
            _picItemLargeIcon.Image = null;
            _lblItemHeaderName.Text = Strings.SelectionNoSelection;
            _lblItemHeaderType.Text = string.Empty;
            _lblItemSize.Text = Strings.DefaultDash;
            _lblItemType.Text = Strings.DefaultDash;
            _lblItemExt.Text = Strings.DefaultDash;
            _lblItemOffset.Text = Strings.DefaultDash;
            _lblItemPieceRange.Text = Strings.DefaultDash;
            _lblItemShare.Text = Strings.DefaultDash;
        }

        #endregion

        #region Theme Management

        private void MainForm_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (_themePreference == ThemePreference.Auto)
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(delegate {
                        bool sysDark = ThemeManager.IsSystemInDarkMode();
                        if (sysDark != _isDarkMode)
                        {
                            ApplyTheme(sysDark);
                            UpdateThemeMenuChecks();
                        }
                    }));
                }
                else
                {
                    bool sysDark = ThemeManager.IsSystemInDarkMode();
                    if (sysDark != _isDarkMode)
                    {
                        ApplyTheme(sysDark);
                        UpdateThemeMenuChecks();
                    }
                }
            }
        }

        private void SetThemePreference(ThemePreference pref)
        {
            _themePreference = pref;
            ThemeManager.SaveThemePreference(_themePreference);
            bool effectiveDark = ThemeManager.ResolveEffectiveDarkMode(_themePreference);
            ApplyTheme(effectiveDark);
            UpdateThemeMenuChecks();
        }

        private void ToggleDarkMode()
        {
            // Toggling explicitly switches between Light and Dark
            _themePreference = _isDarkMode ? ThemePreference.Light : ThemePreference.Dark;
            ThemeManager.SaveThemePreference(_themePreference);
            ApplyTheme(_themePreference == ThemePreference.Dark);
            UpdateThemeMenuChecks();
        }

        private void UpdateThemeMenuChecks()
        {
            if (_menuThemeAuto != null)
            {
                _menuThemeAuto.Checked = (_themePreference == ThemePreference.Auto);
            }
            if (_menuDarkMode != null)
            {
                _menuDarkMode.Checked = _isDarkMode;
            }
        }

        private void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            UpdateThemeMenuChecks();

            Color bgMain = isDark ? ThemeManager.DarkBackground : SystemColors.Control;
            Color bgContent = isDark ? ThemeManager.DarkContentBg : SystemColors.Window;
            Color bgHeader = isDark ? ThemeManager.DarkHeaderBg : SystemColors.Control;
            Color fgPrimary = isDark ? ThemeManager.DarkTextPrimary : SystemColors.WindowText;
            Color fgControl = isDark ? ThemeManager.DarkTextPrimary : SystemColors.ControlText;
            Color fgSecondary = isDark ? ThemeManager.DarkTextSecondary : SystemColors.ControlDarkDark;
            Color fgMuted = isDark ? ThemeManager.DarkTextMuted : SystemColors.GrayText;
            BorderStyle boxBorder = isDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;

            this.BackColor = bgMain;
            this.ForeColor = fgControl;
            if (this.IsHandleCreated)
            {
                ThemeManager.SetWindowDarkMode(this.Handle, isDark);
            }

            if (_clientContainer != null) _clientContainer.BackColor = bgMain;
            if (_splitMain != null) _splitMain.BackColor = isDark ? Color.FromArgb(48, 48, 48) : SystemColors.Control;
            if (_splitCenterAndRight != null) _splitCenterAndRight.BackColor = isDark ? Color.FromArgb(48, 48, 48) : SystemColors.Control;
            if (_splitTreeAndFiles != null) _splitTreeAndFiles.BackColor = isDark ? Color.FromArgb(48, 48, 48) : SystemColors.Control;

            if (_pnlLeftContent != null) _pnlLeftContent.BackColor = bgMain;
            if (_pnlRightContent != null) _pnlRightContent.BackColor = bgMain;

            // Address Bar
            if (_pnlBreadcrumb != null) _pnlBreadcrumb.BackColor = isDark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
            if (_lblAddressTitle != null) _lblAddressTitle.ForeColor = isDark ? Color.FromArgb(170, 170, 170) : SystemColors.ControlText;
            if (_pnlAddressBox != null)
            {
                _pnlAddressBox.BackColor = bgContent;
                _pnlAddressBox.BorderStyle = boxBorder;
            }
            if (_picBreadcrumbIcon != null) _picBreadcrumbIcon.BackColor = bgContent;
            if (_lblBreadcrumb != null)
            {
                _lblBreadcrumb.BackColor = bgContent;
                _lblBreadcrumb.ForeColor = fgPrimary;
            }
            if (_lblFolderSummary != null) _lblFolderSummary.ForeColor = isDark ? Color.FromArgb(170, 170, 170) : SystemColors.ControlText;

            // Section Headers
            for (int i = 0; i < _sectionHeaderPanels.Count; i++)
            {
                _sectionHeaderPanels[i].BackColor = bgHeader;
                _sectionHeaderPanels[i].Invalidate();
            }
            for (int i = 0; i < _sectionHeaderLabels.Count; i++)
            {
                _sectionHeaderLabels[i].ForeColor = isDark ? Color.FromArgb(240, 240, 240) : SystemColors.ControlText;
            }

            // GroupBoxes
            for (int i = 0; i < _allGroupBoxes.Count; i++)
            {
                _allGroupBoxes[i].ForeColor = isDark ? Color.FromArgb(220, 220, 220) : SystemColors.ControlText;
                _allGroupBoxes[i].Invalidate();
            }

            // Property Labels
            for (int i = 0; i < _propertyTitleLabels.Count; i++)
            {
                _propertyTitleLabels[i].ForeColor = fgSecondary;
            }
            for (int i = 0; i < _propertyValueLabels.Count; i++)
            {
                _propertyValueLabels[i].ForeColor = fgControl;
            }

            // TextBoxes
            for (int i = 0; i < _allTextBoxes.Count; i++)
            {
                _allTextBoxes[i].BackColor = bgContent;
                _allTextBoxes[i].ForeColor = fgPrimary;
                _allTextBoxes[i].BorderStyle = boxBorder;
            }

            // ListBox
            if (_lstTrackers != null)
            {
                _lstTrackers.BackColor = bgContent;
                _lstTrackers.ForeColor = fgPrimary;
                _lstTrackers.BorderStyle = boxBorder;
            }

            // Copy Button
            if (_btnCopyHash != null)
            {
                if (isDark)
                {
                    _btnCopyHash.FlatStyle = FlatStyle.Flat;
                    _btnCopyHash.BackColor = ThemeManager.DarkButtonBg;
                    _btnCopyHash.ForeColor = Color.FromArgb(225, 225, 225);
                    _btnCopyHash.FlatAppearance.BorderColor = ThemeManager.DarkButtonBorder;
                }
                else
                {
                    _btnCopyHash.FlatStyle = FlatStyle.Standard;
                    _btnCopyHash.BackColor = SystemColors.Control;
                    _btnCopyHash.ForeColor = SystemColors.ControlText;
                }
            }

            // Right Sidebar Labels
            if (_lblItemHeaderName != null) _lblItemHeaderName.ForeColor = isDark ? Color.FromArgb(245, 245, 245) : SystemColors.ControlText;
            if (_lblItemHeaderType != null) _lblItemHeaderType.ForeColor = fgMuted;
            if (_lblRightPlaceholder != null) _lblRightPlaceholder.ForeColor = fgMuted;

            // TreeView
            if (_treeDirectories != null)
            {
                _treeDirectories.BackColor = bgContent;
                _treeDirectories.ForeColor = fgPrimary;
                _treeDirectories.LineColor = isDark ? ThemeManager.DarkTreeLine : SystemColors.GrayText;
                _treeDirectories.BorderStyle = boxBorder;
                if (isDark)
                {
                    ShellIconHelper.ApplyDarkTheme(_treeDirectories);
                }
                else
                {
                    ShellIconHelper.ApplyClassicTheme(_treeDirectories);
                }
            }

            // ListView
            if (_lvFiles != null)
            {
                _lvFiles.BackColor = bgContent;
                _lvFiles.ForeColor = fgPrimary;
                _lvFiles.BorderStyle = boxBorder;
                _lvFiles.OwnerDraw = isDark;
                if (isDark)
                {
                    ShellIconHelper.ApplyDarkTheme(_lvFiles);
                }
                else
                {
                    ShellIconHelper.ApplyClassicTheme(_lvFiles);
                }
                AttachHeaderHook();
                if (_lvFiles.IsHandleCreated)
                {
                    IntPtr hHeader = ShellIconHelper.SendMessage(_lvFiles.Handle, ShellIconHelper.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                    if (hHeader != IntPtr.Zero)
                    {
                        InvalidateRect(hHeader, IntPtr.Zero, true);
                    }
                }
                _lvFiles.Invalidate();
            }

            // Menus
            if (_menuStrip != null)
            {
                if (isDark)
                {
                    _menuStrip.Renderer = new DarkToolStripRenderer();
                    _menuStrip.BackColor = Color.FromArgb(32, 32, 32);
                    _menuStrip.ForeColor = Color.FromArgb(230, 230, 230);
                }
                else
                {
                    _menuStrip.RenderMode = ToolStripRenderMode.System;
                    _menuStrip.BackColor = SystemColors.Control;
                    _menuStrip.ForeColor = SystemColors.ControlText;
                }
                _menuStrip.Invalidate();
            }

            if (_lvFiles != null && _lvFiles.ContextMenuStrip != null)
            {
                if (isDark)
                {
                    _lvFiles.ContextMenuStrip.Renderer = new DarkToolStripRenderer();
                }
                else
                {
                    _lvFiles.ContextMenuStrip.RenderMode = ToolStripRenderMode.System;
                }
            }

            this.Invalidate(true);
        }

        private void LvFiles_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (!_isDarkMode)
            {
                e.DrawDefault = true;
                return;
            }

            using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 38)))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            using (Pen p = new Pen(Color.FromArgb(58, 58, 58)))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            }
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (e.Header.TextAlign == HorizontalAlignment.Right)
            {
                flags |= TextFormatFlags.Right;
            }
            else
            {
                flags |= TextFormatFlags.Left;
            }
            Rectangle textBounds = new Rectangle(e.Bounds.Left + 4, e.Bounds.Top, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, textBounds, Color.FromArgb(220, 220, 220), flags);
        }

        #region Header Control Dark Painting

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        private DarkHeaderControl _headerHook;

        private void AttachHeaderHook()
        {
            if (_lvFiles != null && _lvFiles.IsHandleCreated)
            {
                IntPtr hHeader = ShellIconHelper.SendMessage(_lvFiles.Handle, ShellIconHelper.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (hHeader != IntPtr.Zero)
                {
                    if (_headerHook == null)
                    {
                        _headerHook = new DarkHeaderControl(() => _isDarkMode);
                    }
                    if (_headerHook.Handle != hHeader)
                    {
                        if (_headerHook.Handle != IntPtr.Zero)
                        {
                            _headerHook.ReleaseHandle();
                        }
                        _headerHook.AssignHandle(hHeader);
                    }
                }
            }
        }

        private void DetachHeaderHook()
        {
            if (_headerHook != null && _headerHook.Handle != IntPtr.Zero)
            {
                _headerHook.ReleaseHandle();
            }
        }

        private class DarkHeaderControl : NativeWindow
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;
            private const int HDM_FIRST = 0x1200;
            private const int HDM_GETITEMCOUNT = HDM_FIRST + 0;
            private const int HDM_GETITEMRECT = HDM_FIRST + 7;

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

            private readonly Func<bool> _isDarkFunc;

            public DarkHeaderControl(Func<bool> isDarkFunc)
            {
                _isDarkFunc = isDarkFunc;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_ERASEBKGND && _isDarkFunc())
                {
                    m.Result = (IntPtr)1;
                    return;
                }

                base.WndProc(ref m);

                if (m.Msg == WM_PAINT && _isDarkFunc())
                {
                    RECT clientRect;
                    if (GetClientRect(this.Handle, out clientRect) && clientRect.Right > 0)
                    {
                        int rightEdge = 0;
                        int count = (int)SendMessage(this.Handle, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                        if (count > 0)
                        {
                            RECT itemRect = new RECT();
                            if (SendMessage(this.Handle, HDM_GETITEMRECT, (IntPtr)(count - 1), ref itemRect) != IntPtr.Zero)
                            {
                                rightEdge = itemRect.Right;
                            }
                        }

                        if (rightEdge < clientRect.Right)
                        {
                            using (Graphics g = Graphics.FromHwnd(this.Handle))
                            {
                                int fillLeft = Math.Max(0, rightEdge);
                                Rectangle emptyRect = new Rectangle(
                                    fillLeft,
                                    0,
                                    clientRect.Right - fillLeft,
                                    clientRect.Bottom
                                );
                                using (SolidBrush b = new SolidBrush(Color.FromArgb(38, 38, 38)))
                                {
                                    g.FillRectangle(b, emptyRect);
                                }
                                using (Pen p = new Pen(Color.FromArgb(58, 58, 58)))
                                {
                                    g.DrawLine(p, emptyRect.Left, emptyRect.Bottom - 1, emptyRect.Right, emptyRect.Bottom - 1);
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Actions & Shortcuts

        private void ActionOpenTorrent()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = Strings.OpenTorrentFilter;
                ofd.Title = Strings.OpenTorrentTitle;
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    LoadTorrent(ofd.FileName);
                }
            }
        }

        private void ActionCloseTorrent()
        {
            _metadata = null;
            this.Text = Strings.AppName;
            _currentDirectory = null;
            _displayItems.Clear();
            _dirToNodeMap.Clear();

            _treeDirectories.Nodes.Clear();
            _lvFiles.Items.Clear();

            _txtTorrentName.Text = string.Empty;
            _txtInfoHash.Text = string.Empty;
            _lblTotalSize.Text = Strings.DefaultDash;
            _lblFilesCount.Text = Strings.DefaultDash;
            _lblPieceSize.Text = Strings.DefaultDash;
            _lblPiecesCount.Text = Strings.DefaultDash;
            _lblPrivacy.Text = Strings.DefaultDash;
            _lblCreatedBy.Text = Strings.DefaultDash;
            _lblCreationDate.Text = Strings.DefaultDash;
            _txtComment.Text = string.Empty;
            _txtPrimaryTracker.Text = string.Empty;
            _lstTrackers.Items.Clear();

            _lblBreadcrumb.Text = Strings.RootPathBackslash;
            _lblFolderSummary.Text = Strings.FolderSummaryEmpty;

            UpdateRightSidebarEmpty();
        }

        private void ActionCopyMagnet()
        {
            if (_metadata != null && !string.IsNullOrEmpty(_metadata.MagnetUri))
            {
                Clipboard.SetText(_metadata.MagnetUri);
            }
        }

        private void ActionCopyInfoHash()
        {
            if (_metadata != null && !string.IsNullOrEmpty(_metadata.InfoHash))
            {
                Clipboard.SetText(_metadata.InfoHash);
            }
        }

        private void ActionCopySelectedName()
        {
            if (_lvFiles.SelectedItems.Count > 0)
            {
                TorrentItem item = _lvFiles.SelectedItems[0].Tag as TorrentItem;
                if (item != null)
                {
                    Clipboard.SetText(item.Name);
                }
            }
        }


        private void ActionCopySelectedSize()
        {
            if (_lvFiles.SelectedItems.Count > 0)
            {
                TorrentItem item = _lvFiles.SelectedItems[0].Tag as TorrentItem;
                if (item != null)
                {
                    Clipboard.SetText(item.SizeDisplay);
                }
            }
        }

        private void ActionSelectAll()
        {
            _lvFiles.BeginUpdate();
            for (int i = 0; i < _lvFiles.Items.Count; i++)
            {
                _lvFiles.Items[i].Selected = true;
            }
            _lvFiles.EndUpdate();
        }

        private void ActionNavigateUp()
        {
            if (_currentDirectory != null && _currentDirectory.Parent != null)
            {
                TorrentDirectory prev = _currentDirectory;
                NavigateToDirectory(_currentDirectory.Parent);

                // Highlight previous folder in the parent list
                for (int i = 0; i < _lvFiles.Items.Count; i++)
                {
                    TorrentItem it = _lvFiles.Items[i].Tag as TorrentItem;
                    if (it != null && it.Directory == prev)
                    {
                        _lvFiles.Items[i].Selected = true;
                        _lvFiles.Items[i].EnsureVisible();
                        _lvFiles.Items[i].Focused = true;
                        break;
                    }
                }
            }
        }

        private void ActionShowAbout()
        {
            MessageBox.Show(this, Strings.AboutMessage, Strings.AboutTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region Drag and Drop

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && files[0].EndsWith(Strings.TorrentFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    LoadTorrent(files[0]);
                }
            }
        }

        #endregion
    }
}
