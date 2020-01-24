using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using monono2.Common;

namespace monono2.AionMonoLib
{
    public class AionLevelViewerImpl
    {
        private GraphicsDevice GraphicsDevice;
        private SpriteFont m_spriteFontCalibri;
        SpriteBatch m_spriteBatch;
        private BasicEffect basicEffect;
        private Matrix view;
        private Matrix projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(75), 1600 / 1200f, 0.1f, 8192f);
        private Camera camera;
        public bool ShowTerrain = true;
        public bool ShowMesh = true;
        public bool ShowCollision = true;
        public bool ShowOriginLines = false;
        public bool ShowFloorLines = true;
        public bool ShowNames = true;
        public int DoorMode = 1; // 0= hide, 1=state1, 2=state2

        //CgfLoader doorHackCgf;
        //int doorHackTicks;

        private RenderData m_renderData = new RenderData();
        
        public event EventHandler<bool> OnChangeMouseVisibilityRequest;
        public event EventHandler OnExitRequest;

        private ContentLoader m_contentLoader;
        private AstarVertexBufferGenerator m_astarLoader;

        public void InvokeOnChangeMouseVisibilityRequest(bool visible)
        {
            OnChangeMouseVisibilityRequest?.Invoke(this, visible);
        }

        public void InvokeOnExitRequest()
        {
            OnExitRequest?.Invoke(this, null);
        }

        private AionLevelViewerImpl(GraphicsDevice GraphicsDevice, IServiceProvider serviceProvider)
        {
            this.GraphicsDevice = GraphicsDevice;
            m_spriteBatch = new SpriteBatch(GraphicsDevice);
            m_spriteFontCalibri = FontUtil.LoadCalibriFont(serviceProvider);

            m_contentLoader = new ContentLoader(GraphicsDevice, m_renderData);
            basicEffect = new BasicEffect(GraphicsDevice);

            camera = new Camera(
                initialPosition: new Vector3(
                    0,-5,3
                    //1900, 2600, 300 // eltnen lepharist slope hill
                    //2484, 2614, 300 // eltnen lepharist
                    //137, 91, 153 // haramel start
                ),
                initialYawDegrees: 180,
                initialPitchDegrees: 50);
            camera.ApplyToView(ref view);
        }

        public static AionLevelViewerImpl CreateLevelViewer(GraphicsDevice GraphicsDevice, 
            IServiceProvider serviceProvider, string aionRoot, string level)
        {
            var result = new AionLevelViewerImpl(GraphicsDevice, serviceProvider);
            result.m_contentLoader.LoadLevelData(aionRoot, level);
            // todo: fix hack - m_astarLoader should be a subobject of whatever manages the NavMeshSet...
            result.m_astarLoader = new AstarVertexBufferGenerator(GraphicsDevice, result.m_contentLoader.GetCompiledNavMeshSet());
            return result;
        }

        public static AionLevelViewerImpl CreateCgfViewer(GraphicsDevice GraphicsDevice,
            IServiceProvider serviceProvider, CgfLoader cgf)
        {
            var result = new AionLevelViewerImpl(GraphicsDevice, serviceProvider);
            result.camera.StepSpeed = 1;
            result.camera.UpDownSpeed = 1;
            result.m_contentLoader.LoadCgf(cgf);
            //result.doorHackCgf = cgf;
            return result;
        }
        
        private string m_testString = "";
        public string GetTestString()
        {
            return m_testString;
        }
        
        public void SetAstarTargetPoint()
        {
            if (m_astarLoader != null)
                m_astarLoader.SetTargetPoint(camera.GetCameraPosition(), m_renderData, ref m_testString);
        }

        public void SetAstarStartPoint()
        {
            if (m_astarLoader != null)
                m_astarLoader.SetStartPoint(camera.GetCameraPosition(), m_renderData, ref m_testString);
        }

        public void DrawNavMeshUnderPosition()
        {
            m_contentLoader.LoadNavMeshGridUnderPosition(camera.GetCameraPosition());
        }

        public void HandleInput(bool isActive, GameTime gameTime)
        {
            GameInput.HandleInput(this, isActive, gameTime, camera, ref view);
        }
        
        public string GetCurrentCameraPosition()
        {
            return camera.ToString();
        }

        public void SetProjection(int width, int height)
        {
            projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(75), width / (float)height, 0.1f, 8192f);
        }
        
        public void Draw(GraphicsDevice GraphicsDevice)
        {
            /*
            // hack: animate doors at each keyframe
            m_renderData.labels.Clear();
            m_contentLoader.LoadCgfDoorHack(doorHackCgf, doorHackTicks);
            doorHackTicks += 160;
            if (doorHackTicks > 55555)
                doorHackTicks = 0;*/

            GraphicsDevice.Clear(new Color(66,66,66));//Color.CornflowerBlue);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

            //basicEffect.World = world;
            basicEffect.View = view;
            basicEffect.Projection = projection;
            basicEffect.VertexColorEnabled = true;
            //basicEffect.Texture = texture1;
            //     basicEffect.TextureEnabled = true;
            basicEffect.FogEnabled = true;
            basicEffect.FogStart = 10f;
            basicEffect.FogEnd = 2500f;
            basicEffect.FogColor = //new Vector3(0xa0/255.0f, 0xe0/255.0f,1);
                new Vector3(0.8f, 0, 0.4f);

            
            if (ShowMesh && m_renderData.vertexBuffer != null)
            {
                foreach (var vb in m_renderData.vertexBuffer)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }
            
            if (ShowMesh && m_renderData.vegetationVertexBuffer != null)
            {
                foreach (var vb in m_renderData.vegetationVertexBuffer)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }

            // Doors - no collision

            if (DoorMode == 1 && m_renderData.doorVertexBuffer1 != null)
            {
                foreach (var vb in m_renderData.doorVertexBuffer1)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }
            else if (DoorMode == 2 && m_renderData.doorVertexBuffer2 != null)
            {
                foreach (var vb in m_renderData.doorVertexBuffer2)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }


            if (ShowTerrain && m_renderData.terrainVertexBuffer != null)
            {
                
                basicEffect.EnableDefaultLighting();
              //  basicEffect.DirectionalLight0.DiffuseColor = new Vector3(1, 1, 1); // white
              //  basicEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(1, 0, 0.5f));  // from the x-axis
              //  basicEffect.DirectionalLight0.SpecularColor = new Vector3(0, 1, 0); // green highlight
                foreach (var vb in m_renderData.terrainVertexBuffer)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
                basicEffect.LightingEnabled = false;
            }

            GraphicsDevice.BlendState = BlendState.Additive;

            if (ShowCollision && m_renderData.collisionVertexBuffer != null)
            {
                foreach (var vb in m_renderData.collisionVertexBuffer)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }

            if (DoorMode == 1 && m_renderData.doorCollisionVertexBuffer1 != null)
            {
                foreach (var vb in m_renderData.doorCollisionVertexBuffer1)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }
            else if (DoorMode == 2 && m_renderData.doorCollisionVertexBuffer2 != null)
            {
                foreach (var vb in m_renderData.doorCollisionVertexBuffer2)
                {
                    GraphicsDevice.SetVertexBuffer(vb);
                    foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, vb.VertexCount / 3);
                    }
                }
            }

            GraphicsDevice.BlendState = BlendState.Opaque;


            basicEffect.FogStart = 10f;
            basicEffect.FogEnd = 400f;
            basicEffect.FogColor = new Vector3(0, 0, 0);

            // Clear depth buffer to draw lines over the geometry.
            GraphicsDevice.Clear(ClearOptions.DepthBuffer, Color.Black, 1.0f, 0);

            if (ShowOriginLines && m_renderData.lineVertexBuffer != null && m_renderData.lineVertexBuffer.VertexCount > 0)
            {
                GraphicsDevice.SetVertexBuffer(m_renderData.lineVertexBuffer);
                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, m_renderData.lineVertexBuffer.VertexCount / 2);
                }
            }

            if (ShowFloorLines && m_renderData.floorLineVertexBuffer != null && m_renderData.floorLineVertexBuffer.VertexCount > 0)
            {
                GraphicsDevice.SetVertexBuffer(m_renderData.floorLineVertexBuffer);
                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, m_renderData.floorLineVertexBuffer.VertexCount / 2);
                }
            }

            if (ShowFloorLines && m_renderData.astarLineVertexBuffer != null && m_renderData.astarLineVertexBuffer.VertexCount > 0)
            {
                GraphicsDevice.SetVertexBuffer(m_renderData.astarLineVertexBuffer);
                foreach (EffectPass pass in basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, m_renderData.astarLineVertexBuffer.VertexCount / 2);
                }
            }
            

            if (ShowNames)
            {
                foreach (var label in m_renderData.labels)
                {
                    if (Vector3.Distance(camera.GetCameraPosition(), label.position) > 300)
                        continue;

                    var frustum = new BoundingFrustum(view * projection);

                    if (frustum.Contains(label.position) == ContainmentType.Contains)
                    {
                        // Note: SpriteBatch.Begin() modifies the rendering state.
                        // Full rendering state needs to get reset at start of Draw().
                        m_spriteBatch.Begin();
                        Vector3 pos2d = GraphicsDevice.Viewport.Project(label.position, projection, view, Matrix.Identity);
                        var textPos = new Vector2((float)Math.Round(pos2d.X), (float)Math.Round(pos2d.Y));
                        m_spriteBatch.DrawString(m_spriteFontCalibri, label.text, textPos, Color.White);

                        m_spriteBatch.End();
                    }
                }
            }
        }
    }
}
