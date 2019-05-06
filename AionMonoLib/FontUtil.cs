using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace monono2.AionMonoLib
{
    public static class FontUtil
    {
        private class CalibriLoader : ContentManager
        {
            public CalibriLoader(IServiceProvider serviceProvider) : base(serviceProvider)
            {
            }

            // intercept OpenStream and force it to read a specific embedded resource.
            protected override Stream OpenStream(string assetName)
            {
                return Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    Assembly.GetExecutingAssembly().GetManifestResourceNames()
                        .Single(s => s.EndsWith(".calibri10.xnb")));
            }

            public SpriteFont LoadFont()
            {
                return base.ReadAsset<SpriteFont>("calibri", null);
            }
        }

        public static SpriteFont LoadCalibriFont(IServiceProvider serviceProvider)
        {
            return new CalibriLoader(serviceProvider).LoadFont();
        }
    }
}
