 void MakeStatistics()
{
  unsigned int i ;
  
  // compute CPM
  if (Counts.BufferFull)
    {
    for(i=0, Counts.CPM=0L ; i < MAX_COUNT_HISTORY ; i++)
      Counts.CPSmean60 = Counts.CPM += Counts.CPS[i] ;
    Counts.CPSmean60 /= MAX_COUNT_HISTORY ;
    } 
  else
    {
    if (Counts.head > 1)  // check to be sure there is no division by 0!
      {
      for(i=0, Counts.CPM=0L ; i < Counts.head ; i++)
        Counts.CPSmean60 = Counts.CPM += Counts.CPS[i] ;
      Counts.CPM = (60L * Counts.CPM) / (Counts.head-1) ;
      Counts.CPSmean60 /= (Counts.head-1) ;
      }
    }
  if (Counts.TotalSeconds > 0)
    {
    Counts.CPSmean = (float)Counts.TotalCounts / (float)Counts.TotalSeconds ;
    }
}

/**
 * pin maping:
 * 2 - digital input
 * 3 - digital input
 * 4 - digital input
 * 5 - digital input
 * 6 - digital output
 * 7 - digital output
 * 8 - digital output
 * 9 - digital output
 * 10 - analog output
 * 11 - analog output
 * 14 - analog input
 * 15 - analog input
 *
 * pin 13 reservado para ver el estado de la comunicación
 */
void io_setup() {
  pinMode(2, INPUT);
  pinMode(3, INPUT);
  pinMode(4, INPUT);
  pinMode(5, INPUT);
  pinMode(6, OUTPUT);
  pinMode(7, OUTPUT);
  pinMode(8, OUTPUT);
  pinMode(9, OUTPUT);
  pinMode(10, OUTPUT);
  pinMode(11, OUTPUT);
  pinMode(13, OUTPUT);

  digitalWrite(6, LOW );
  digitalWrite(7, LOW );
  digitalWrite(8, LOW );
  digitalWrite(9, LOW );
  digitalWrite(13, HIGH ); //Led del pin 13 de la placa
  analogWrite(10, 0 ); //PWM 0%
  analogWrite(11, 0 ); //PWM 0%
}

/*********************************************************
Enlaza la tabla de registros con los pines
*********************************************************/
void io_poll() {
  // digital inputs -> au16data[0]
  // Lee las entradas digitales y las guarda en bits de la primera variable del vector
  // (es lo mismo que hacer una máscara)
  bitWrite( au16data[0], 0, digitalRead( 2 )); //Lee el pin 2 de Arduino y lo guarda en el bit 0 de la variable au16data[0] 
  bitWrite( au16data[0], 1, digitalRead( 3 ));
  bitWrite( au16data[0], 2, digitalRead( 4 ));
  bitWrite( au16data[0], 3, digitalRead( 5 ));

  // digital outputs -> au16data[1]
  // Lee los bits de la segunda variable y los pone en las salidas digitales
  digitalWrite( 6, bitRead( au16data[1], 0 )); //Lee el bit 0 de la variable au16data[1] y lo pone en el pin 6 de Arduino
  digitalWrite( 7, bitRead( au16data[1], 1 ));
  digitalWrite( 8, bitRead( au16data[1], 2 ));
  digitalWrite( 9, bitRead( au16data[1], 3 ));

  // Cambia el valor del PWM
  analogWrite( 10, au16data[2] ); //El valor de au16data[2] se escribe en la salida de PWM del pin 10 de Arduino. (siendo 0=0% y 255=100%)
  analogWrite( 11, au16data[3] );

  // Lee las entradas analógicas (ADC)
  au16data[4] = analogRead( 0 ); //El valor analógico leido en el pin A0 se guarda en au16data[4]. (siendo 0=0v y 1023=5v)
  au16data[5] = analogRead( 1 );

  // Diagnóstico de la comunicación (para debug)
  au16data[6] = slave.getInCnt();  //Devuelve cuantos mensajes se recibieron
  au16data[7] = slave.getOutCnt(); //Devuelve cuantos mensajes se transmitieron
  au16data[8] = slave.getErrCnt(); //Devuelve cuantos errores hubieron
}

static int32_t readVref()
{
#ifdef __LL_ADC_CALC_VREFANALOG_VOLTAGE
#ifdef STM32U5xx
  return (__LL_ADC_CALC_VREFANALOG_VOLTAGE(ADC1, analogRead(AVREF), LL_ADC_RESOLUTION));
#else
  return (__LL_ADC_CALC_VREFANALOG_VOLTAGE(analogRead(AVREF), LL_ADC_RESOLUTION));
#endif
#else
  return (VREFINT * ADC_RANGE / analogRead(AVREF)); // ADC sample to mV
#endif
}


#ifdef ATEMP
static int32_t readTempSensor(int32_t VRef)
{
#ifdef __LL_ADC_CALC_TEMPERATURE
#ifdef STM32U5xx
  return (__LL_ADC_CALC_TEMPERATURE(ADC1, VRef, analogRead(ATEMP), LL_ADC_RESOLUTION));
#else
  return (__LL_ADC_CALC_TEMPERATURE(VRef, analogRead(ATEMP), LL_ADC_RESOLUTION));
#endif
#elif defined(__LL_ADC_CALC_TEMPERATURE_TYP_PARAMS)
  return (__LL_ADC_CALC_TEMPERATURE_TYP_PARAMS(AVG_SLOPE, VTEMP, CALX_TEMP, VRef, analogRead(ATEMP), LL_ADC_RESOLUTION));
#else
  return 0;
#endif
}
#endif


static int32_t readVoltage(int32_t VRef, uint32_t pin)
{
#ifdef STM32U5xx
  return (__LL_ADC_CALC_DATA_TO_VOLTAGE(ADC1, VRef, analogRead(pin), LL_ADC_RESOLUTION));
#else
  return (__LL_ADC_CALC_DATA_TO_VOLTAGE(VRef, analogRead(pin), LL_ADC_RESOLUTION));
#endif
}

// Interrupt service routine called when convesion is complete (BUSY goes LOW)
void ADC_ISR()     
{
for (uint8_t k = 0; k < 8; k++)   // read all 8 channels of AD7606
	{ 
	    //GPIOB->ODR &= ~(0b0000000000010000);  //Clear RD PB4
			digitalWrite(RD, LOW);
      
      Data[k] = GPIOA->IDR & 0x00FF;   //0x00FF is 0b 0000 0000 1111 1111  read from PA0 to PA7 at once
      Data[k] = Data[k] << 8;          // first MSB is out (DB14 is set HIGH) so shift it 8 bit to the right
      
      //GPIOB->ODR |= 0b0000000000010000;  // Set RD PB4
      //GPIOB->ODR &= ~(0b0000000000010000);  //Clear RD PB4
      digitalWrite(RD, HIGH);
      digitalWrite(RD, LOW);
      Data[k] += GPIOA->IDR & 0x00FF;   //0x00FF is 0b 0000 0000 1111 1111  read from PA0 to PA7 at once and add to Data[k] the LSB
      
      //GPIOB->ODR |= 0b0000000000010000;  // Set RD PB4
      digitalWrite(RD, HIGH);
		
	}
}

float movingAverage_ins(float value) {
  const byte nvalues = 8;             // Moving average window size

  static byte current = 0;            // Index for current value
  static byte cvalues = 0;            // Count of values read (<= nvalues)
  static float sum = 0;               // Rolling sum
  static float values[nvalues];

  sum += value;

  // If the window is full, adjust the sum by deleting the oldest value
  if (cvalues == nvalues)
    sum -= values[current];

  values[current] = value;          // Replace the oldest with the latest

  if (++current >= nvalues)
    current = 0;

  if (cvalues < nvalues)
    cvalues += 1;

  return sum/cvalues;
}

float movingAverage_sen(float value) {
  const byte nvalues = 8;             // Moving average window size

  static byte current = 0;            // Index for current value
  static byte cvalues = 0;            // Count of values read (<= nvalues)
  static float sum = 0;               // Rolling sum
  static float values[nvalues];

  sum += value;

  // If the window is full, adjust the sum by deleting the oldest value
  if (cvalues == nvalues)
    sum -= values[current];

  values[current] = value;          // Replace the oldest with the latest

  if (++current >= nvalues)
    current = 0;

  if (cvalues < nvalues)
    cvalues += 1;

  return sum/cvalues;
}