namespace OpticEMS.Contracts.Services.Etching
{
    public class State
    {
        public int Index { get; set; }
        public double Reference { get; set; }
        public double Threshold { get; set; }
        public double WindowStartTime { get; set; }
        public ProcessState ProcessState { get; set; } = ProcessState.Idle;
        public int ConsecutiveOut { get; set; }
        public int ConsecutiveIn { get; set; }
        public int DetectionWindowTime { get; set; }
        public int WindowInCount { get; set; }
        public int WindowOutCount { get; set; }
        public bool HasReachedWindowIn { get; set; }
    }
}
