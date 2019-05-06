using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using monono2.AionMonoLib;

namespace monono2
{
    public class Game1 : Game
    {
        string m_gameDir;
        string m_levelFolder;
        GraphicsDeviceManager graphics;
        AionLevelViewerImpl content;

        public Game1(string gameDir, string levelFolder)
            : base()
        {
            m_gameDir = gameDir;
            m_levelFolder = levelFolder;

            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1600;
            graphics.PreferredBackBufferHeight = 1200;
            graphics.PreferMultiSampling = true;
            Window.AllowUserResizing = true;
            IsFixedTimeStep = false; // this fixes random mouse lag problems
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.IsMouseVisible = true;
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            content = AionLevelViewerImpl.CreateLevelViewer(GraphicsDevice, Services, m_gameDir, m_levelFolder);
            content.SetProjection(GraphicsDevice.PresentationParameters.BackBufferWidth, GraphicsDevice.PresentationParameters.BackBufferHeight);

            content.OnChangeMouseVisibilityRequest += 
                (sender, vis) => { IsMouseVisible = vis; };
            content.OnExitRequest += 
                (sender, e) => { Exit(); };
        }

        /*private void InitTexture1()
        {
            var pixels = new Color[4 * 4];
            for (int i = 0; i < 4 * 4; i += 4)
            {
                pixels[i] = new Color(255, 255, 255, 255);
                pixels[i + 1] = new Color(255, 0, 0, 255);
                pixels[i + 2] = new Color(0, 255, 0, 255);
                pixels[i + 3] = new Color(0, 0, 255, 255);
            }
            texture1 = new Texture2D(GraphicsDevice, 4, 4);
            texture1.SetData<Color>(pixels);
        }*/

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
        }

        int m_updateCount = 0;
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            m_updateCount++;
            content.HandleInput(IsActive, gameTime);
            if (m_updateCount % 30 == 0)
                base.Window.Title = content.GetCurrentCameraPosition()
                    + content.GetTestString();
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            content.Draw(GraphicsDevice);
        }
    }
}
