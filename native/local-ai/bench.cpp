#include <cstdio>
#include <cstdlib>
extern "C" void *tsai_load(const char *, int, int, int);
extern "C" void tsai_free(void *);
extern "C" const char *tsai_error();
extern "C" int tsai_generate(void *, const char *, int, int, int(*)(const char*,int,void*), void *, double *);
int main(int argc, char **argv) {
    if(argc != 2) return 2;
    auto engine = tsai_load(argv[1], 512, 2, 0);
    if(!engine) { fprintf(stderr, "%s\n", tsai_error()); return 1; }
    for(int repeat = 0; repeat < 3; ++repeat) {
        double speed = 0;
        int count = tsai_generate(engine, "Once upon a time", 64, 1, nullptr, nullptr, &speed);
        printf("{\"backend\":\"native\",\"run\":%d,\"tokens\":%d,\"tokens_per_second\":%.3f}\n", repeat, count, speed);
        if(count != 64) { fprintf(stderr, "%s\n", tsai_error()); return 1; }
    }
    tsai_free(engine);
}
