# BBC micro:bit V2 / MicroPython. Flash with https://python.microbit.org/
from microbit import *
import math

# Digital horn, active-low (button connects input to GND).
PIN_NUMBERS = [0, 1, 2, 8, 12, 13, 14, 15, 16]
PINS = [pin0, pin1, pin2, pin8, pin12, pin13, pin14, pin15, pin16]
ACTIVE_LOW = True
FULL_TURN_DEGREES = 45
uart.init(baudrate=115200)
index = 0

def horn_down():
    return PINS[index].read_digital() == (0 if ACTIVE_LOW else 1)

def show_pin():
    display.scroll(str(PIN_NUMBERS[index]), wait=False)

# A cycles candidates; horn shows H; B confirms. No game input during setup.
PINS[index].set_pull(PINS[index].PULL_UP)
show_pin()
was_horn = False
while True:
    if button_a.was_pressed():
        index = (index + 1) % len(PINS)
        PINS[index].set_pull(PINS[index].PULL_UP)
        show_pin()
    pressed = horn_down()
    if pressed and not was_horn:
        display.show('H')
    elif was_horn and not pressed:
        show_pin()
    was_horn = pressed
    if button_b.was_pressed():
        break
    sleep(20)
while button_a.is_pressed() or button_b.is_pressed() or horn_down():
    sleep(20)

def angle():
    return math.atan2(accelerometer.get_x(), -accelerometer.get_y()) * 180 / math.pi

# LEDs face driver, USB connector at top. Confirm B with wheel centered.
center = angle()
display.show(Image.YES)
count = 0
previous = False
raw_previous = False
stable_at = running_time()
filtered = 0.0
calibrate_at = None
calibrated = False
while True:
    now = running_time()
    both = button_a.is_pressed() and button_b.is_pressed()
    if both:
        if calibrate_at is None:
            calibrate_at = now
        if now - calibrate_at >= 800 and not calibrated:
            center = angle()
            filtered = 0.0
            calibrated = True
            display.show(Image.YES)
    else:
        calibrate_at = None
        calibrated = False
    degrees = (angle() - center + 180) % 360 - 180
    target = max(-1000, min(1000, int(degrees * 1000 / FULL_TURN_DEGREES)))
    filtered += (target - filtered) * 0.35
    pressed = horn_down()
    if pressed != raw_previous:
        raw_previous = pressed
        stable_at = now
    if pressed != previous and now - stable_at >= 40:
        previous = pressed
        if pressed and not both:
            count = (count + 1) % 65536
    uart.write('TT1,%d,%d\n' % (int(filtered), count))
    sleep(20)
