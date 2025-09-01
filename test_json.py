from __future__ import annotations

import argparse
import json
import socket
import struct
import sys
import time
from dataclasses import dataclass
from typing import Optional, Tuple


MAGIC_16 = 18458  # 16 channel output
MAGIC_32 = 29569  # 32 channel output
PKT_LEN_16 = 2 + 2 + 4 + 16 * 2  # 40 bytes
PKT_LEN_32 = 2 + 2 + 4 + 32 * 2  # 72 bytes


@dataclass
class SitlOutput:
    magic: int
    frame_rate: int  # Hz (requested step rate)
    frame_count: int
    pwm: Tuple[int, ...]

    @property
    def channels(self) -> int:
        return len(self.pwm)


def parse_sitl_packet(data: bytes) -> Optional[SitlOutput]:
    """Parse a SITL output packet. Returns None if not valid."""
    if len(data) not in (PKT_LEN_16, PKT_LEN_32):
        return None
    magic, frame_rate, frame_count = struct.unpack_from("<HHI", data, 0)
    if magic == MAGIC_16 and len(data) == PKT_LEN_16:
        pwm_fmt = "<16H"
        pwm = struct.unpack_from(pwm_fmt, data, 8)
    elif magic == MAGIC_32 and len(data) == PKT_LEN_32:
        pwm_fmt = "<32H"
        pwm = struct.unpack_from(pwm_fmt, data, 8)
    else:
        return None
    return SitlOutput(
        magic=magic, frame_rate=frame_rate, frame_count=frame_count, pwm=pwm
    )


class JsonPhysicsBackend:
    def __init__(self, port: int = 9002, verbose: bool = False, bind: str = "0.0.0.0"):
        self.port = port
        self.verbose = verbose
        self.bind = bind
        self.sock: Optional[socket.socket] = None
        self.start_time_monotonic = time.perf_counter()
        self.first_frame_count: Optional[int] = None
        self.last_timestamp: float = 0.0
        self.connected = False
        self.peer = None  # (ip, port)

    def log(self, *args):
        if self.verbose:
            print(*args, file=sys.stderr, flush=True)

    def open(self):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        # Allow quick restart
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind((self.bind, self.port))
        self.log(f"Listening on {self.bind}:{self.port} ...")

    def close(self):
        if self.sock:
            self.sock.close()
            self.sock = None

    def compute_timestamp(self, pkt: SitlOutput) -> float:
        """Compute physics time (seconds). Use frame_count/frame_rate when sensible, else monotonic."""
        if pkt.frame_rate > 0:
            if self.first_frame_count is None:
                self.first_frame_count = pkt.frame_count
            # Use difference to allow SITL restarts
            dt = (pkt.frame_count - self.first_frame_count) / float(pkt.frame_rate)
            if dt >= self.last_timestamp:  # monotonic progression
                self.last_timestamp = dt
                return dt
        # Fallback monotonic since start
        dt = time.perf_counter() - self.start_time_monotonic
        if dt < self.last_timestamp:
            dt = self.last_timestamp  # ensure non-decreasing
        self.last_timestamp = dt
        return dt

    def build_json_frame(self, timestamp: float) -> str:
        frame = {
            "timestamp": round(timestamp, 6),  # microsecond-ish resolution
            "imu": {"gyro": [0, 0, 0], "accel_body": [0, 0, 0]},
            "position": [0, 0, 0],  # NED meters
            "attitude": [0, 0, 0],  # roll, pitch, yaw radians
            "velocity": [0, 0, 0],  # NED m/s
        }
        return json.dumps(frame, separators=(",", ":")) + "\n"

    def serve_forever(self):
        if self.sock is None:
            self.open()
        assert self.sock is not None
        while True:
            try:
                data, addr = self.sock.recvfrom(256)
            except KeyboardInterrupt:
                self.log("Interrupted, shutting down.")
                break
            except Exception as e:
                self.log(f"Socket error: {e}")
                continue

            pkt = parse_sitl_packet(data)
            if not pkt:
                self.log(
                    f"Ignoring packet len={len(data)} from {addr} hex={data.hex()}"
                )
                continue

            if not self.connected:
                self.connected = True
                self.peer = addr
                self.log(f"Established JSON SITL link with {addr}")

            self.log(
                "SITL RX:"
                f" magic={pkt.magic}"
                f" frame_rate={pkt.frame_rate}"
                f" frame_count={pkt.frame_count}"
                f" channels={pkt.channels}"
                f" pwm={list(pkt.pwm)}"
            )

            timestamp = self.compute_timestamp(pkt)
            response = self.build_json_frame(timestamp)
            try:
                sent = self.sock.sendto(response.encode("utf-8"), addr)
                self.log(
                    f"Frame {pkt.frame_count} ({pkt.channels}ch) rate={pkt.frame_rate}Hz -> {addr} ts={timestamp:.6f} bytes={sent}"
                )
            except Exception as e:
                self.log(f"Send error to {addr}: {e}")


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="ArduPilot JSON SITL test physics backend"
    )
    parser.add_argument(
        "--port", type=int, default=9002, help="UDP listen port (default 9002)"
    )
    parser.add_argument(
        "--bind", type=str, default="0.0.0.0", help="Bind address (default 0.0.0.0)"
    )
    parser.add_argument(
        "-v", "--verbose", action="store_true", help="Verbose logging to stderr"
    )
    args = parser.parse_args(argv)

    backend = JsonPhysicsBackend(port=args.port, verbose=args.verbose, bind=args.bind)
    try:
        backend.open()
        backend.serve_forever()
    finally:
        backend.close()


if __name__ == "__main__":
    main()
