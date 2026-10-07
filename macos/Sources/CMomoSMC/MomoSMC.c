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

int momo_smc_read_watts(uint32_t connection, const char *key, double *watts) {
    if (!connection || !key || strlen(key) != 4 || !watts) return 0;
    SMCMessage request = {0}, response = {0};
    for (int i = 0; i < 4; i++) request.key = (request.key << 8) | (uint8_t)key[i];
    request.command = 9; // Read key metadata.
    size_t size = sizeof(response);
    if (IOConnectCallStructMethod(connection, 2, &request, sizeof(request), &response, &size)
        != KERN_SUCCESS || response.result || size != sizeof(response)) return 0;
    if (response.info.size != 4 || response.info.type != 0x666c7420) return 0; // "flt "
    request.info.size = response.info.size;
    request.command = 5; // Read bytes; never issue a write command.
    size = sizeof(response);
    if (IOConnectCallStructMethod(connection, 2, &request, sizeof(request), &response, &size)
        != KERN_SUCCESS || response.result || size != sizeof(response)) return 0;
    float value;
    memcpy(&value, response.bytes, sizeof(value));
    if (!isfinite(value) || value < 0 || value > 2000) return 0;
    *watts = value; // A valid zero is preserved.
    return 1;
}
