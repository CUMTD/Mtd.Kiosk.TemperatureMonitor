import usb_cdc

# - console=True enables the REPL (for development/debugging)
# - data=True enables a second port purely for data
usb_cdc.enable(console=False, data=True)
