using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace monono2.AionMonoLib
{
    public class KeyStateWatcher
    {
        Keys m_key;
        bool s_down = false;
        public KeyStateWatcher(Keys key)
        {
            m_key = key;
        }
        
        public bool IsDown(ref KeyboardState kb)
        {
            if (!s_down && kb.IsKeyDown(m_key))
            {
                s_down = true;
                return true;
            }
            else if (s_down && kb.IsKeyUp(m_key))
                s_down = false;
            return false;
        }
    }

    public static class GameInput
    {
        static bool s_mouseDown = false;
        static int s_mouseStartX = 0, s_mouseStartY = 0;
        static KeyStateWatcher s_ksw1 = new KeyStateWatcher(Keys.D1);
        static KeyStateWatcher s_ksw2 = new KeyStateWatcher(Keys.D2);
        static KeyStateWatcher s_ksw3 = new KeyStateWatcher(Keys.D3);
        static KeyStateWatcher s_ksw4 = new KeyStateWatcher(Keys.D4);
        static KeyStateWatcher s_ksw5 = new KeyStateWatcher(Keys.D5);
        static KeyStateWatcher s_ksw6 = new KeyStateWatcher(Keys.D6);
        static KeyStateWatcher s_ksw7 = new KeyStateWatcher(Keys.D7);

        static KeyStateWatcher s_kswt = new KeyStateWatcher(Keys.T);
        static KeyStateWatcher s_kswy = new KeyStateWatcher(Keys.Y);
        static KeyStateWatcher s_kswm = new KeyStateWatcher(Keys.M);
        private static bool s_wasPreviouslyActive;
        
        public static void HandleInput(AionLevelViewerImpl game, bool isActive, GameTime gameTime, Camera camera, ref Matrix view)
        {
            if (!isActive)
            {
                s_wasPreviouslyActive = false;
                s_mouseDown = false;
                return;
            }

            var kb = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || kb.IsKeyDown(Keys.Escape))
                game.InvokeOnExitRequest();

            float speedMult = 0.04f;
            if (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift))
                speedMult = 0.7f;

            if (kb.IsKeyDown(Keys.W))
            {
                camera.Forward(speedMult);
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.S))
            {
                camera.Forward(-speedMult);
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.A))
            {
                camera.Right(speedMult);
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.D))
            {
                camera.Right(-speedMult);
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.Q))
            {
                camera.Turn(-MathHelper.ToRadians(2.5f));
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.E))
            {
                camera.Turn(MathHelper.ToRadians(2.5f));
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.R))
            {
                camera.MoveUp(speedMult);
                camera.ApplyToView(ref view);
            }
            if (kb.IsKeyDown(Keys.F))
            {
                camera.MoveUp(-speedMult);
                camera.ApplyToView(ref view);
            }

            // navmesh
            if (s_kswt.IsDown(ref kb))
            {
                game.SetAstarTargetPoint();
            }
            if (s_kswy.IsDown(ref kb))
            {
                game.SetAstarStartPoint();
            }
            if (s_kswm.IsDown(ref kb))
            {
                game.DrawNavMeshUnderPosition();
            }
            
            // toggles
            if (s_ksw1.IsDown(ref kb))
                game.ShowTerrain ^= true;
            if (s_ksw2.IsDown(ref kb))
                game.ShowMesh ^= true;
            if (s_ksw3.IsDown(ref kb))
                game.ShowCollision ^= true;
            if (s_ksw4.IsDown(ref kb))
                game.ShowOriginLines ^= true;
            if (s_ksw5.IsDown(ref kb))
                game.ShowFloorLines ^= true;
            if (s_ksw6.IsDown(ref kb))
                game.ShowNames ^= true;
            if (s_ksw7.IsDown(ref kb))
                game.DoorMode = (game.DoorMode + 1) % 3; // hide doors, show state 1, show state 2

            if (s_wasPreviouslyActive)
            {
                var ms = Mouse.GetState();
                if (ms.LeftButton == ButtonState.Pressed)
                {
                    if (!s_mouseDown)
                    {
                        s_mouseDown = true;
                        s_mouseStartX = ms.X;
                        s_mouseStartY = ms.Y;
                        
                        game.InvokeOnChangeMouseVisibilityRequest(false);
                    }
                    else
                    {
                        int dx = s_mouseStartX - ms.X;
                        int dy = s_mouseStartY - ms.Y;
                        
                        if (dx != 0 || dy != 0)
                        {
                            if (dx != 0)
                                camera.Turn(MathHelper.ToRadians(-dx) / 4f);
                            if (dy != 0)
                                camera.Pitch(MathHelper.ToRadians(dy) / 4f);

                            camera.ApplyToView(ref view);

                            Mouse.SetPosition(s_mouseStartX, s_mouseStartY);
                        }
                    }
                }
                else if (s_mouseDown)
                {
                    s_mouseDown = false;
                    game.InvokeOnChangeMouseVisibilityRequest(true);
                }
            }
            s_wasPreviouslyActive = true;
        }
    }
}
