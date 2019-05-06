using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Forms.Controls;
using monono2.AionMonoLib;
using monono2.Common;

namespace monono2.AionClientViewer
{
    public class AionLevelViewerControl : UpdateWindow
    {
        // HACK - public so it can be swapped out for fake resizing.
        public AionLevelViewerImpl m_game;

        // HACK - monogame crashes in Mouse.SetPosition() if no Game has been created, so create a dummy one.
        private Game hackmousefix = new Game();

        private string m_aionClientRoot;
        private string m_levelFolder;

        private CgfLoader m_cgf;
        
        public AionLevelViewerControl(AionLevelViewerImpl game)
        {
            m_game = game;
        }
        
        // TODO configure better... load level OR cgf, not both
        public AionLevelViewerControl(string aionClientRoot, string levelFolder, CgfLoader cgf)
        {
            m_aionClientRoot = aionClientRoot;
            m_levelFolder = levelFolder;
            m_cgf = cgf;
        }

        protected override void Initialize()
        {
            if (m_game == null)
            {
                if (m_cgf != null)
                    m_game = AionLevelViewerImpl.CreateCgfViewer(GraphicsDevice, Services, m_cgf);
                else
                    m_game = AionLevelViewerImpl.CreateLevelViewer(GraphicsDevice, Services, m_aionClientRoot, m_levelFolder);
            }
            base.Initialize();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
        }

        private int m_updateCount;
        public event EventHandler<string> OnUpdateTitle;

        protected override void Update(GameTime gameTime)
        {
            m_game.HandleInput(Focused, gameTime);

            if (OnUpdateTitle != null && m_updateCount++ % 30 == 0)
            {
                OnUpdateTitle.Invoke(this, m_game.GetCurrentCameraPosition()
                    + m_game.GetTestString());
            }

            base.Update(gameTime);
        }


        protected override void Draw()
        {
            m_game.Draw(Editor.graphics);
        }
    }
}
