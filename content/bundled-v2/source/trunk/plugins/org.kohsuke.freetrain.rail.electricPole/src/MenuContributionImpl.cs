using System;
using System.Windows.Forms;
using System.Xml;
using freetrain.framework;
using freetrain.contributions.others;

namespace freetrain.world.rail.pole
{
	// temporary
	// TODO: instead of having our own menu item,
	// merge them to rail accessory builder form.
	
	public class MenuContributionImpl : MenuContribution
	{
		public MenuContributionImpl( XmlElement e ) : base(e) {}

		public override void mergeMenu( MainMenu containerMenu ) {
			MenuItem mi = new MenuItem("‰Ëü’Œ...",new EventHandler(onClick));
			containerMenu.MenuItems[2].MenuItems.Add(mi);

            mi = new MenuItem("‰Ëü’Œ‚ğ”ñ•\¦", new EventHandler(onDisabledClick));
            mi.Checked = !Core.options.drawElectlicPoles;
            containerMenu.MenuItems[1].MenuItems.Add(mi);
        }

        private void onDisabledClick(object sender, EventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            Core.options.drawElectlicPoles = !Core.options.drawElectlicPoles;
            mi.Checked = !Core.options.drawElectlicPoles; 
        }

		private void onClick( object sender, EventArgs e ) {
			new ControllerForm().Show();
		}
	}
}
