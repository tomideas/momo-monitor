#include "MomoSMC.h"
#include <IOKit/IOKitLib.h>
#include <mach/mach.h>
#include <stddef.h>
#include <string.h>
#include <math.h>

// Wire layout of AppleSMC's read-key user client protocol, not a public Apple API.
typedef struct {
    uint32_t key;
    struct { uint8_t major, minor, build, reserved; uint16_t release; } version;
    struct { uint16_t version, length; uint32_t cpu, gpu, memory; } limits;
    struct { uint32_t size, type; uint8_t attributes; } info;
    uint8_t result, status, command;
    uint32_t index;
    uint8_t bytes[32];
} SMCMessage;
_Static_assert(sizeof(SMCMessage) == 80, "AppleSMC message size");
_Static_assert(offsetof(SMCMessage, bytes) == 48, "AppleSMC payload offset");

uint32_t momo_smc_open(void) {
    io_service_t service = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("AppleSMC"));
    if (!service) return 0;
    io_connect_t connection = 0;
    kern_return_t result = IOServiceOpen(service, mach_task_self(), 0, &connection);
    IOObjectRelease(service);
    return result == KERN_SUCCESS ? connection : 0;
}
void momo_smc_close(uint32_t connection) { if (connection) IOServiceClose(connection); }

static int read_payload(uint32_t connection, const char *key, uint32_t *type, uint32_t *length, uint8_t bytes[32]) {
    if (!connection || !key || strlen(key) != 4) return 0;
    SMCMessage request = {0}, response = {0};
    for (int i = 0; i < 4; i++) request.key = (request.key << 8) | (uint8_t)key[i];
    request.command = 9; // Read key metadata.
    size_t size = sizeof(response);
    if (IOConnectCallStructMethod(connection, 2, &request, sizeof(request), &response, &size)
        != KERN_SUCCESS || response.result || size != sizeof(response)) return 0;
    if (!response.info.size || response.info.size > 32) return 0;
    *type = response.info.type; *length = response.info.size;
    request.info.size = response.info.size;
    request.command = 5; // Read bytes; never issue a write command.
    size = sizeof(response);
    if (IOConnectCallStructMethod(connection, 2, &request, sizeof(request), &response, &size)
        != KERN_SUCCESS || response.result || size != sizeof(response)) return 0;
    memcpy(bytes, response.bytes, *length);
    return 1;
}

int momo_smc_decode_number(uint32_t type, const uint8_t *bytes, size_t size, double *value) {
    if (!bytes || !value) return 0;
    double decoded;
    switch (type) {
        case 0x666c7420: { // flt: little-endian IEEE float on supported Apple Silicon.
            if (size != 4) return 0;
            float raw; memcpy(&raw, bytes, 4); decoded = raw; break;
        }
        case 0x73703738: { // sp78: signed big-endian fixed point, 8 fractional bits.
            if (size != 2) return 0;
            uint16_t raw = ((uint16_t)bytes[0] << 8) | bytes[1];
            decoded = (raw >= 0x8000 ? (int32_t)raw - 65536 : raw) / 256.0; break;
        }
        case 0x66706532: // fpe2: unsigned fixed point, 2 fractional bits (fan RPM).
            if (size != 2) return 0;
            decoded = (((uint16_t)bytes[0] << 8) | bytes[1]) / 4.0; break;
        case 0x75693820: // ui8: fan count, including valid zero on fanless Macs.
            if (size != 1) return 0;
            decoded = bytes[0]; break;
        case 0x75693136:
            if (size != 2) return 0;
            decoded = ((uint16_t)bytes[0] << 8) | bytes[1]; break;
        case 0x75693332:
            if (size != 4) return 0;
            decoded = ((uint32_t)bytes[0] << 24) | ((uint32_t)bytes[1] << 16) | ((uint32_t)bytes[2] << 8) | bytes[3]; break;
        default: return 0;
    }
    if (!isfinite(decoded)) return 0;
    *value = decoded; return 1;
}

int momo_smc_read_number(uint32_t connection, const char *key, double *value) {
    uint32_t type = 0, length = 0; uint8_t bytes[32] = {0};
    return read_payload(connection, key, &type, &length, bytes) && momo_smc_decode_number(type, bytes, length, value);
}

int momo_smc_read_watts(uint32_t connection, const char *key, double *watts) {
    uint32_t type = 0, length = 0; uint8_t bytes[32] = {0}; double value;
    if (!watts || !read_payload(connection, key, &type, &length, bytes) || type != 0x666c7420 ||
        !momo_smc_decode_number(type, bytes, length, &value) || value < 0 || value > 2000) return 0;
    *watts = value; return 1;
}
