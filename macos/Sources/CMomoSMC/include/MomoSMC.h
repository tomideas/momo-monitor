#ifndef MOMO_SMC_H
#define MOMO_SMC_H
#include <stdint.h>

// Read-only AppleSMC connection. No write command is exposed.
uint32_t momo_smc_open(void);
void momo_smc_close(uint32_t connection);
int momo_smc_read_watts(uint32_t connection, const char *key, double *watts);
#endif
