/*
* Set the registers for Counter 1
* Records pulse input on pin PA8*/

void initCounter1(){
TIM1->CCMR1 |= 0x0001; // Ch. 1 as TI1
TIM1->SMCR |= 0x0007; // Ext. clk mode 1
TIM1->SMCR |= 0x0050; // TI1FP1 is the trigger
TIM1->CR1 |= 0x0001; // enable counting
}