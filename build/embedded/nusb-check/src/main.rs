//! Runs nusb's unchanged Linux enumeration against the private permissioned sysfs inventory.
//! This proof validates enumeration, not a physical probe, ioctl transport or target flashing.
use nusb::MaybeFuture;

fn main() {
    let expected: Vec<String> = std::env::args().skip(1).collect();
    assert_eq!(expected.len(), 2, "supply expected hexadecimal VID and PID");
    let vid = u16::from_str_radix(&expected[0], 16).expect("VID");
    let pid = u16::from_str_radix(&expected[1], 16).expect("PID");
    let devices: Vec<_> = nusb::list_devices().wait().expect("nusb enumeration").collect();
    assert_eq!(devices.len(), 1, "only the selected granted device must be visible");
    let device = &devices[0];
    assert_eq!(device.vendor_id(), vid);
    assert_eq!(device.product_id(), pid);
    assert_eq!(device.interfaces().count(), 1);
    assert_eq!(device.interfaces().next().unwrap().interface_number(), 0);
    println!("NUSB_PERMISSIONED_ENUMERATION_PASSED {:04x}:{:04x}", vid, pid);
}
