// Vastior manager - all UI dimensions in one place (base pixels at 96 DPI).
// Every size is scaled through VastiorTheme.S() at use, so the window stays
// crisp and correctly sized on high-DPI displays.
namespace Vastior
{
	static class VastiorLayout
	{
		// window + multi-layer frame
		public const int WindowWidth = 560;
		public const int Frame = 6;           // total brass frame thickness
		public const int FrameBlackInset = 2; // black layer start
		public const int FrameInnerInset = 5; // inner brass line start
		public const int BottomPad = 6;

		// shared horizontal padding inside the panel
		public const int PadX = 12;

		// heading block (title + subtitle)
		public const int HeadingTop = 7;
		public const int HeadingBlock = 36;   // vertical space the heading occupies

		// game folder row
		public const int FieldLabelWidth = 78;
		public const int FieldHeight = 27;
		public const int FieldTextInsetX = 9;
		public const int FieldEdgeWidth = 3;  // brass accent on the field's left edge
		public const int BrowseWidth = 78;
		public const int BrowseGap = 8;

		// status row (label + badge share one baseline)
		public const int FieldToStatus = 10;
		public const int StatusRowHeight = 22;
		public const int StatusLabelWidth = 52;
		public const int StatusLabelGap = 10;
		public const int BadgeWidth = 128;
		public const int BadgeDot = 8;
		public const int BadgePadX = 9;
		public const int BadgeDotTextGap = 9;

		// console
		public const int StatusToConsole = 10;
		public const int ConsoleHeight = 138;
		public const int ConsoleFrame = 2;
		public const int ConsoleInsetX = 8;
		public const int ConsoleInsetY = 7;

		// button rows
		public const int ConsoleToActions = 9;
		public const int ActionRowHeight = 31;
		public const int ActionsToUtil = 5;
		public const int UtilRowHeight = 28;
		public const int ButtonGap = 7;

		// footer
		public const int UtilToFooter = 7;
		public const int FooterHeight = 26;

		// custom button shape
		public const int ChamferCut = 6;

		// themed scrollbar
		public const int ScrollBarWidth = 12;
		public const int ScrollThumbMin = 26;
		public const int ScrollWheelLines = 3;
	}
}
