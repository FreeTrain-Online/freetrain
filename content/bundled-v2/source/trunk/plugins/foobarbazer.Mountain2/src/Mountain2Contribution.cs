using System;
using System.Xml;
using freetrain.contributions.structs;

namespace freetrain.controllers.terrain
{
	[Serializable]
	public class Mountain2ContributionImpl : SpecialStructureContribution
	{
		public Mountain2ContributionImpl( XmlElement e ) : base(e) {
			theInstance = this;
		}

		internal static Mountain2ContributionImpl theInstance;

		public override string name { get { return "地形操作（範囲指定）"; } }

		public override string oneLineDescription {
			get {
				return "指定範囲を一括して隆起または掘削します";
			}
		}

		public override void showDialog() {
			Mountain2Controller.create();
		}
	}
}
