import time
import board
import adafruit_sht4x
import neopixel
import usb_cdc

update_interval_seconds = 5
led = neopixel.NeoPixel(board.NEOPIXEL, 1, brightness=0.2)

# wait for host to connect
while not usb_cdc.data.connected:
    time.sleep(1)

# blink LED on connection
led[0] = (255,0,0)
time.sleep(.2)
led[0] = (0,255,0)
time.sleep(.2)
led[0] = (0,0,255)
time.sleep(.2)
led[0] = (0,0,0)

i2c = board.I2C()
sht = adafruit_sht4x.SHT4x(i2c)
sht.mode = adafruit_sht4x.Mode.NOHEAT_HIGHPRECISION
console = usb_cdc.data

# write temp and humidity data to data console, blink on every loop
while True:
    temperature, humidity = sht.measurements
    temperature = round(temperature)
    humidity = round(humidity)
    console.write(bytes([255, temperature, humidity]))
    led[0] = (0, 47, 135)  # blink MTD Blue
    led[0] = (0, 0, 0)  # off

    time.sleep(update_interval_seconds)
