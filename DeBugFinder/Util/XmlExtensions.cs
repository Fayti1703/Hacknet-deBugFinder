using System.Xml;

namespace DeBugFinder.Util {
	public static class XmlExtensions {
		public static string toLogString(this XmlReader rdr) {
			return rdr.EOF
				? "Reader reached EOF"
				: "Reader at " +
				  (rdr.IsEmptyElement ? "Self-closing" : "") +
				  $"{rdr.NodeType.ToString()} Node '{rdr.Name}'";
		}
	}
}
