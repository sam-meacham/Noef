using System.IO;
using System.Text;


namespace Noef.CodeGen.Ui
{
	/// <summary>
	/// Defaults:
	///		string: TextBox
	///		bool: CheckBox
	///		enum: DropDownList
	///		int: ? (TextBox?)
	///		float: ? (TextBox?)
	///		DateTime: JqDatepicker
	/// </summary>
	public enum UiControlType
	{
		None, // no UI component
		LabelDiv,
		LabelSpan,

		// standard html controls
		TextBox, // default for string
		CheckBox, // default for bool
		RadioList,
		DropDownList, // default for enum
		TextArea,

		// standard jquery UI
		JqSlider,
		JqDatepicker, // default for DateTime

		// 3rd party jquery UI
		JqColorpicker,
	}
}
