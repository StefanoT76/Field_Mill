


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Libraries
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#include <ModbusRtu.h>
#include <stm32yyxx_ll_adc.h>
#include <FlashStorage_STM32.h>

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Global Variables
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


// If you want to debug uncomment the following line

//#define DEBUG

// internal temp reading
/* Values available in datasheet */
#if defined(STM32C0xx)
#define CALX_TEMP 30
#else
#define CALX_TEMP 25
#endif

#if defined(STM32C0xx)
#define VTEMP      760
#define AVG_SLOPE 2530
#define VREFINT   1212
#elif defined(STM32F1xx)
#define VTEMP     1430
#define AVG_SLOPE 4300
#define VREFINT   1200
#elif defined(STM32F2xx) || defined(STM32F4xx)
#define VTEMP      760
#define AVG_SLOPE 2500
#define VREFINT   1210
#endif

#define LL_ADC_RESOLUTION LL_ADC_RESOLUTION_12B
#define ADC_RANGE 4096

//AD7606 init
#define DB0 PA0
#define DB1 PA1
#define DB2 PA2
#define DB3 PA3
#define DB4 PA4
#define DB5 PA5
#define DB6 PA6
#define DB7 PA7 

#define CVA_CVB PB5
#define CS PB3
#define RD PB4
#define RESET PB8
#define BUSY PB6

#define OS0 PB1
#define OS1 PB10
#define OS2 PB11
#define RANGE PB7


volatile int16_t Data[8];    // 8 16bit int for ADC results
int16_t Zero[2] = {0,0};     // 2 16bit int for ADC zero

unsigned long i = 0;
int eeAddress = 0;

boolean OverFlow[2] = {false,false};

uint16_t SW_Version = 96;         //SW version

#define ID   1      //node ID. 0 for master, 1-247 for slave

// data array for modbus network sharing RS485
uint16_t au16data[24];
int8_t state = 0;
unsigned long tempus;

//union float for RS485 data exchange
typedef union
{
  float number;
  uint16_t bytes[2];
} FLOATUNION_t;


int32_t SETPOINT = 10;
int32_t INTERNAL_TEMP = 0;
/**
 *  Modbus object declaration
 *  u8id : node id = 0 for master, = 1..247 for slave
 *  port : serial port
 *  u8txenpin : 0 for RS-232 and USB-FTDI 
 *               or any pin number > 1 for RS-485
 */
Modbus slave(ID,Serial1,PB0); // this is slave @1 and RS-485 PA9-->DI  PA10-->RO  PB0-->DE/RE# control
 
boolean ROTOR_SPEED_OK = false;
boolean HEART_BEAT = false;

FLOATUNION_t Electric_Field_insensitive;
FLOATUNION_t Electric_Field_insensitive_cal;
FLOATUNION_t offset_insensitive;

FLOATUNION_t Electric_Field_sensitive;
FLOATUNION_t Electric_Field_sensitive_cal;
FLOATUNION_t offset_sensitive;



struct TIMER {
  unsigned char tmp_millisec ;
  unsigned char centisecs ;
  unsigned char secs ;
  unsigned char mins ;
  unsigned char hours ;
  unsigned char days ;
  //------------------
  unsigned char tmp_state ;
  unsigned char state ;  // used in switch/case state machine (1/10s)
  unsigned int stateFlags ; // used to run ONLY once for second
} 
  Timer = {
  0,0,0,0,0,0,0,0, 0xFFFF} 
;

// number of previous measurements (every one second) to be stored (60=1min)
#define  MAX_COUNT_HISTORY  60
struct COUNTS {
  unsigned char head ;  // CPS queue pointers
  unsigned char BufferFull ;    // boolean: true if all CPS[MAX_COUNT_HISTORY] are used
  unsigned char BufferOverflow ;// how many times 16 bit counter has overflown? Max count=2^24/s (16777216 counts/s)
  unsigned long CPSmean60 ;       // Counts per Second # sliding windows mean (in 60 seconds) 
  unsigned long CPM ;           // Counts in last Minute # sliding windows mean
  unsigned long TotalCounts ;   // Number of TOTAL counts received to date
  unsigned long TotalSeconds ;  // Number of seconds from start of acquisition (max=136years!)
  float CPSmean ;               // CPS mean
  unsigned long CPS[MAX_COUNT_HISTORY] ; // Counts Per Second (last MAX_COUNT_HISTORY samples)
} 
Counts = {
  0,0,0, 0UL,0UL,0UL,0UL, 0.0} 
;


// Hardware timer object initialization

HardwareTimer timer1(TIM1);

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  SETUP
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


void setup() {

analogReadResolution(12);  //for correct temp reading

//Pins definitions  

  pinMode(PA8, INPUT);      // Rotation Pulse
  
  pinMode(PB0, OUTPUT);     //DE/RE# RS485 control
  pinMode(PB9, OUTPUT);     //heart beat
  pinMode(PB15, OUTPUT);    //Zero Ralay active HIGH
  pinMode(PB13, OUTPUT);    //Motor enable active HIGH
  pinMode(PB12, OUTPUT);    //Level traslator enble
  pinMode(PB14, OUTPUT);    //Heater on/off

  
  
  pinMode(PC13, OUTPUT);          // communication OK led


  pinMode(DB0, INPUT);            //ADC data pins
  pinMode(DB1, INPUT);
  pinMode(DB2, INPUT);
  pinMode(DB3, INPUT);
  pinMode(DB4, INPUT);
  pinMode(DB5, INPUT);
  pinMode(DB6, INPUT);
  pinMode(DB7, INPUT);

  pinMode(BUSY, INPUT);           //ADC control pins

  pinMode(CVA_CVB, OUTPUT);       //CONVST A and B tied toghether
  pinMode(RD, OUTPUT);
  pinMode(CS, OUTPUT);
  pinMode(RESET, OUTPUT);
  pinMode(OS0, OUTPUT);
  pinMode(OS1, OUTPUT);
  pinMode(OS2, OUTPUT);
  pinMode(RANGE, OUTPUT);

  digitalWrite(RESET, HIGH);     // reset ADC
  digitalWrite(RESET, LOW);
  digitalWrite(CVA_CVB, LOW);
  digitalWrite(RD, HIGH);
  digitalWrite(CS, LOW);
  digitalWrite(RANGE, HIGH);    // set range to 10V
  digitalWrite(OS0, LOW);       // set Oversampling to 64
  digitalWrite(OS1, HIGH);
  digitalWrite(OS2, HIGH);

  
  attachInterrupt(BUSY, ADC_ISR, FALLING);
  
  
  Serial1.begin( 19200, SERIAL_8E1 );   // 19200 baud, 8-bits, even, 1-bit stop
  slave.start();
  
  tempus = millis() + 100; //actual time + 100ms
  digitalWrite(PC13, LOW ); //Turn on led on PC13 of BLuePill
  
  
  //Initialize the counters
  initCounter1();  // init Timer1 as a Counter (on pin PA8)

  //Set the counter to 0
  timer1.setCount(0);

  //disable heater
  digitalWrite(PB14,LOW);
  bitWrite( au16data[9], 2, 0);

  //Enable level translator
  digitalWrite(PB12,HIGH);

  //Short plates
  digitalWrite(PB15,HIGH);
  bitWrite( au16data[9], 0, 1);
  
  while (i < 500000){   //pause about 1 sec
  
  GPIOB->ODR |= 0b0000000000100000;  // Set CONVSTA/CONVSTB PB5
  GPIOB->ODR &= ~(0b0000000000100000);  //Clear CONVSTA/CONVSTB PB5 to start conversion
  
  i++;
  }
     
  
  while (i < 500000){
  
  GPIOB->ODR |= 0b0000000000100000;  // Set CONVSTA/CONVSTB PB5
  GPIOB->ODR &= ~(0b0000000000100000);  //Clear CONVSTA/CONVSTB PB5 to start conversion
    
  Zero[0] = 0x0000; //Data[0];  //disabled as is not reliable offset is always 0
  Zero[1] = 0x0000; //Data[1];
  
  i++;
  }

  //Clear Short
  digitalWrite(PB15,LOW);
  bitWrite( au16data[9], 0, 0);
  
  //Start motor
  digitalWrite(PB13,HIGH);
  bitWrite( au16data[9], 1, 1);

  Electric_Field_insensitive.number = 0.0;
  Electric_Field_sensitive.number = 0.0;

  ////////////////////////////////////////////////////// Default values for gain and offset  ////////////////////////////////////////////////////////////////////////////

  Electric_Field_insensitive_cal.number = 2000.0;
  Electric_Field_sensitive_cal.number = 20000.0;

  offset_insensitive.number = 0.0;
  offset_sensitive.number = 0.0;
  
  /////////////////////////////////////////////////////  load cal & offset data from EEPROM  ////////////////////////////////////////////////////////////////////////////
  
  EEPROM.get(eeAddress, Electric_Field_sensitive_cal.number);
  eeAddress += sizeof(float);
  au16data[13] = (Electric_Field_sensitive_cal.bytes[0]);                //update input register for calibration with data from EEPROM
  au16data[14] = (Electric_Field_sensitive_cal.bytes[1]);

  EEPROM.get(eeAddress, Electric_Field_insensitive_cal.number);
  eeAddress += sizeof(float);
  au16data[1] = (Electric_Field_insensitive_cal.bytes[0]);                //update input register for calibration with data from EEPROM
  au16data[2] = (Electric_Field_insensitive_cal.bytes[1]);

  EEPROM.get(eeAddress, offset_sensitive.number);
  eeAddress += sizeof(float);
  au16data[15] = (offset_sensitive.bytes[0]);                //update input register for sensitive offset with data from EEPROM
  au16data[16] = (offset_sensitive.bytes[1]);

  EEPROM.get(eeAddress, offset_insensitive.number);
  au16data[17] = (offset_insensitive.bytes[0]);                //update input register for insensitive offset with data from EEPROM
  au16data[18] = (offset_insensitive.bytes[1]);

  eeAddress = 0;
}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//  MAIN LOOP
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


void loop() 
{
 
const float adcSens = -0.000305; //-305uV per bit in -+10V range, the amplifier is inverting type



// read RS485 for commands

state = slave.poll( au16data, 24 );    //check input buffer
                                      //Parameters: Table of records for the exchange of information
                                      // Size of the records table
                                      //Returns 0 if there is no request for data
                                      //Returns 1 to 4 if there was a communication error
                                      //Returns more than 4 if the request was processed correctly

if (state > 4) { //If it is greater than 4 = the request was correct
    tempus = millis() + 50; //actual time + 50ms
    digitalWrite(PC13, LOW);// led ON
  }
  if (millis() > tempus) digitalWrite(PC13, HIGH );//Turn led OFF after 50ms

//////////////////////////////////////////// read from ADC's //////////////////////////////////////////////////////////////////////

int32_t VRef = readVref();          //Read internal T

GPIOB->ODR |= 0b0000000000100000;  // Set CONVSTA/CONVSTB PB5
GPIOB->ODR &= ~(0b0000000000100000);  //Clear CONVSTA/CONVSTB PB5 to start conversion

//////////////////////////////////////////// INSENSITIVE CHANNEL //////////////////////////////////////////////////////////////////

Electric_Field_insensitive.number = movingAverage_ins( ((float) adcSens * (float) Electric_Field_insensitive_cal.number * (float) (Data[0] - Zero[0])) - (float) offset_insensitive.number);


au16data[3] = (Electric_Field_insensitive.bytes[0]);    //update data table float 32bit divided into two 16 bit parts
au16data[4] = (Electric_Field_insensitive.bytes[1]);


if (Data[0] < -32500 || Data[0] > 32500){               //overflow insensitive
OverFlow[0] = true;
bitWrite( au16data[9], 8, 1);}
else{
OverFlow[0] = false;
bitWrite( au16data[9], 8, 0);}

////////////////////////////////////////////// SENSITIVE CHANNEL //////////////////////////////////////////////////////////////////

Electric_Field_sensitive.number = movingAverage_sen( ((float) adcSens * (float) Electric_Field_sensitive_cal.number * (float) (Data[1] - Zero[1])) - (float) offset_sensitive.number) ;


au16data[5] = (Electric_Field_sensitive.bytes[0]);    //update data table float 32bit divided into two 16 bit parts
au16data[6] = (Electric_Field_sensitive.bytes[1]);

if (Data[1] < -32500 || Data[1] > 32500){             //overflow sensitive
OverFlow[1] = true;
bitWrite( au16data[9], 9, 1);}
else{
OverFlow[1] = false;
bitWrite( au16data[9], 9, 0);}

/////////////////////////////////////////////// LOW SPEED TASKS /////////////////////////////////////////////////////////////////////

  if (Timer.stateFlags & (0x001 << Timer.state))  // every schedule step run only ONCE in every second!  it's true only if Timer.stateFlags = 0xFFFF it happens once every second
    {
    Timer.stateFlags &= (~(0x001 << Timer.state)) ;  // clean flag: so we do not repeat more than one time in this second! &= put the result of bitwise AND between Timer.stateFlags and (~(0x001 << Timer.state) into Timer.stateFlags  disabling the execution until next second

    switch (Timer.state) {
      case 0: 
              // TotalCounts can be updated in the main loop and not in the IRQ routine
              Counts.TotalCounts += (unsigned long) Counts.CPS[Counts.head] ;
              
              // Compute statistics
              MakeStatistics() ;
              
              // Flashing HEART_BEAT LED
              if (HEART_BEAT){  
              digitalWrite(PB9,LOW);  //Turn LED ON
              HEART_BEAT = false;}
              else
              {  
              digitalWrite(PB9,HIGH);  //Turn LED OFF
              HEART_BEAT = true;}
              break ;
              
      case 1: // Check motor speed from the counter
         
              if (Counts.CPS[Counts.head] > 75 && Counts.CPS[Counts.head] < 85)
              ROTOR_SPEED_OK = true;
              break ;
  
      case 2: //  
              if(ROTOR_SPEED_OK)


              break ;
              
              
      case 3: // 
              if(ROTOR_SPEED_OK) 
             
              
              break ;
      
      case 4: // 
      
      
      
             
              break ;
      
      case 5: //// Serial output for comunications and debug
          #ifdef DEBUG
          
              Serial1.print("  Sec=") ;
              Serial1.print(Counts.TotalSeconds) ;        // Seconds elapsed from start of acquisition
              
              Serial1.print("  CPS=") ;
              Serial1.print(Counts.CPS[Counts.head]) ;   // counts read in previous second
              
              Serial1.print("  CPS60=") ;
              Serial1.print(Counts.CPSmean60) ;                // Counts per minute (mean of last 60 CPS readings)
              
              Serial1.print("  CPSmean=") ;
              Serial1.print((unsigned long) (Counts.CPSmean)) ;                // Counts per minute (mean of all readings)
              
              Serial1.print("  CPM60=") ;
              Serial1.print(Counts.CPM) ;                // Counts per minute (mean of last 60 CPM readings)
              
              Serial1.print("  CPMmean=") ;
              Serial1.print((unsigned long)(Counts.CPSmean * 60.0)) ;                // Counts per minute (mean of all readings)
              
              Serial1.print("  TotCounts=") ;
              Serial1.print(Counts.TotalCounts) ;      // total counts 
                
              Serial1.print("  Count.head=") ;
              Serial1.print(Counts.head);
               
              Serial1.print("  RPM=") ;
              Serial1.println(Counts.CPSmean60 / 2); // this should be about 1800-1900 RPM
              
          #endif

              au16data[0] = Counts.CPS[Counts.head];                          //16 bit rotor speed

              (Electric_Field_insensitive_cal.bytes[0]) = au16data[1];        //input register for insensitive channel calibration float 32bit divided into two 16 bit parts
              (Electric_Field_insensitive_cal.bytes[1]) = au16data[2];

              au16data[7] = (readTempSensor(VRef));                           //signed long 32bit divided into two 16 bit parts internal temp
              au16data[8] = (readTempSensor(VRef) >> 16);
              
              //au16data[9]     input register 16 bit  is #10 in the master interface

              digitalWrite(PB15, bitRead(au16data[9],0));  // Short (1) - clear (0) plates
              digitalWrite(PB13, bitRead(au16data[9],1));  // Start (1) - Stop (0) motor
              digitalWrite(PB14, bitRead(au16data[9],2));  // Enable (1) - Disable (0) heater

              if (bitRead(au16data[9],3)){                 //EEPROM calibration & offset write
              digitalWrite(PB9,LOW);
              EEPROM.put(eeAddress,Electric_Field_sensitive_cal.number);
              eeAddress += sizeof(float);
              EEPROM.put(eeAddress,Electric_Field_insensitive_cal.number);
              eeAddress += sizeof(float);
              EEPROM.put(eeAddress,offset_sensitive.number);
              eeAddress += sizeof(float);
              EEPROM.put(eeAddress,offset_insensitive.number);
              eeAddress = 0;
              bitWrite( au16data[9], 3, 0);
              digitalWrite(PB9,HIGH); }

              
              au16data[10] = Zero[0];                                     //Returns last Zero reading
              au16data[11] = Zero[1];        
              au16data[12] = slave.getErrCnt();                           //Returns how many errors there were
              
              (Electric_Field_sensitive_cal.bytes[0]) = au16data[13];     //input register for sensitive channel calibration float 32bit divided into two 16 bit parts
              (Electric_Field_sensitive_cal.bytes[1]) = au16data[14];

              (offset_sensitive.bytes[0]) = au16data[15];                 //input register for sensitive channel offset float 32bit divided into two 16 bit parts
              (offset_sensitive.bytes[1]) = au16data[16];

              (offset_insensitive.bytes[0]) = au16data[17];               //input register for insensitive offset float 32bit divided into two 16 bit parts
              (offset_insensitive.bytes[1]) = au16data[18];
              au16data[19] = SW_Version;                                  // Returns the SW version



              break ;
      
      case 6: // Internal temp check and heater control
            
            INTERNAL_TEMP = readTempSensor(VRef);
            
           if (INTERNAL_TEMP < SETPOINT){
                digitalWrite(PB14,HIGH);   // enable Heater
                bitWrite( au16data[9], 2, 1);}
            else{
                digitalWrite(PB14,LOW);     // disable Heater
                bitWrite( au16data[9], 2, 0);}


          
              break ;
      
      case 7: // 
      
      
            
              break ;
      
      case 8: 
             
      
      
              break ;
      
      case 9: //
      
           
      
              break ;
     
      } 
      ;
    }

}


