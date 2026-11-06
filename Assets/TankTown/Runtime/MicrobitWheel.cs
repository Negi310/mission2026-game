using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace TankTown
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MicrobitWheelState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('M', 'B', 'W', 'L');
        [InputControl(name = "steering", layout = "Axis")]
        public float steering;
        [InputControl(name = "horn", layout = "Button", bit = 0)]
        public uint buttons;
        [InputControl(name = "angle", layout = "Axis")]
        public float angle;
        [InputControl(name = "piezo", layout = "Integer")]
        public int piezo;
    }

    [InputControlLayout(stateType = typeof(MicrobitWheelState), displayName = "micro:bit Wheel")]
    public sealed class MicrobitWheel : InputDevice
    {
        public static MicrobitWheel current { get; private set; }
        public AxisControl steering { get; private set; }
        public ButtonControl horn { get; private set; }
        public AxisControl angle { get; private set; }
        public IntegerControl piezo { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup();
            steering = GetChildControl<AxisControl>("steering");
            horn = GetChildControl<ButtonControl>("horn");
            angle = GetChildControl<AxisControl>("angle");
            piezo = GetChildControl<IntegerControl>("piezo");
        }
        public override void MakeCurrent() { base.MakeCurrent(); current = this; }
        protected override void OnRemoved()
        {
            if (current == this) current = null;
            base.OnRemoved();
        }
        internal static MicrobitWheel Create()
        {
            if (InputSystem.LoadLayout(nameof(MicrobitWheel)) == null) InputSystem.RegisterLayout<MicrobitWheel>();
            var device = InputSystem.AddDevice<MicrobitWheel>(); device.MakeCurrent(); return device;
        }
        internal void Submit(float value, bool fire, float rawAngle = 0, int rawPiezo = 0)
        {
            // Press/release in one update preserves a tap even on consecutive frames.
            if (fire) InputSystem.QueueStateEvent(this, new MicrobitWheelState { steering = value, buttons = 1, angle = rawAngle, piezo = rawPiezo });
            InputSystem.QueueStateEvent(this, new MicrobitWheelState { steering = value, angle = rawAngle, piezo = rawPiezo });
        }
    }
}
