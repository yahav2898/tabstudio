//! Runs nusb's unchanged Linux enumeration against the private permissioned sysfs inventory.
//! This proof validates enumeration, usbfs open and descriptor reads on a borrowed regular-file FD.
//! A regular file is not a pollable USB device: the kernel must refuse nusb's epoll registration.
//! No physical probe, USB transfer or target flashing is claimed.
use nusb::MaybeFuture;
use std::os::raw::{c_int, c_ulong};

extern "C" {
    fn ioctl(fd: c_int, request: c_ulong, ...) -> c_int;
}

fn main() {
    let expected: Vec<String> = std::env::args().skip(1).collect();
    let unbridged_control = expected.len() == 3 && expected[2] == "--unbridged-control";
    assert!(expected.len() == 2 || unbridged_control, "supply expected hexadecimal VID and PID");
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

    // The private /dev placeholder is empty. Reaching this exact error proves that nusb opened the
    // selected usbfs path through PRoot's raw-syscall FD duplication and read valid descriptors from
    // the inherited file. A missing path, a denied open or reading the placeholder fail earlier.
    let error = match device.open().wait() {
        Ok(_) => panic!("a regular descriptor fixture unexpectedly opened as a real USB device"),
        Err(error) => error,
    };
    if unbridged_control {
        assert_eq!(error.os_error(), None, "the private placeholder must fail descriptor validation: {error}");
        assert_eq!(error.to_string(), "invalid device descriptor");
        println!("NUSB_UNBRIDGED_EMPTY_PLACEHOLDER_REJECTED_PASSED");
        return;
    }
    assert_eq!(error.os_error(), Some(1), "expected kernel EPERM from epoll on a regular file: {error}");
    assert!(error.to_string().starts_with("failed to add epoll watch"), "unexpected open failure: {error}");
    println!("NUSB_PERMISSIONED_OPEN_REACHED_KERNEL_EPOLL_PASSED errno=1");

    // Independently confirm that no USB ioctl is emulated or reported successful on this fixture.
    // Linux USBDEVFS_GET_CAPABILITIES = _IOR('U', 26, u32). This read-only ioctl must return ENOTTY.
    let mut capabilities: u32 = 0;
    let result = unsafe { ioctl(3, 0x8004_551a, &mut capabilities as *mut u32) };
    assert_eq!(result, -1, "a regular file must not report USB capabilities");
    assert_eq!(std::io::Error::last_os_error().raw_os_error(), Some(25));
    println!("USBFS_FIXTURE_IOCTL_REACHED_KERNEL_ENOTTY_PASSED errno=25");
}
