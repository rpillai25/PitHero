using Nez;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// Base scene class that fixes render target disposal issue when scenes transition.
    /// All game scenes should inherit from this instead of Nez.Scene directly.
    /// 
    /// Root Cause: The Nez framework's Scene.End() method disposes render targets without
    /// unbinding them first. When a scene transition happens during Update (before Draw),
    /// the render target set by Scene.Update() is still bound, causing FNA to throw
    /// "Disposing target that is still bound" exception.
    /// 
    /// Fix: This class overrides End() to unbind render targets (SetRenderTarget(null))
    /// before calling base.End(), preventing the disposal of bound render targets.
    /// </summary>
    public abstract class BaseScene : Scene
    {
        /// <summary>
        /// Override of Scene.End() that properly unbinds render targets before disposal.
        /// This prevents "Disposing target that is still bound" exceptions when transitioning scenes.
        /// </summary>
        public override void End()
        {
            // Unbind any active render targets before the base Scene.End() disposes them
            // This is necessary because Scene.Update() sets the render target, but when a scene
            // transition happens during Update, Scene.PostRender() never gets called to unbind it
            Core.GraphicsDevice.SetRenderTarget(null);
            
            base.End();
        }
    }
}

