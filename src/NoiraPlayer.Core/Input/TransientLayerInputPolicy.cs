namespace NoiraPlayer.Core.Input
{
    public static class TransientLayerInputPolicy
    {
        public static bool ShouldDismiss(bool layerVisible, bool backKeyPressed)
        {
            return layerVisible && backKeyPressed;
        }
    }
}
