namespace AniWorldAutoDL_Webpanel.Models
{
    public class ConvertProgressModel
    {
        public float Size { get; set; }
        public TimeSpan Time { get; set; }
        public float Bitrate { get; set; }
        public float Speed { get; set; }
        public float FPS { get; set; }
        public int ProgressPercent { get; set; }
    }
}
