/**
* SysTick is used for generating a 1ms timer tick: 
* 
**/
void HAL_SYSTICK_Callback()  // call this from the systick interrupt every millisecond


{
  
if (++Timer.tmp_millisec >= 10)  // tick interval is in 1ms units   this is true every 10ms, the variable Timer.tmp_millisec is always incremented
    {
    Timer.tmp_millisec = 0 ;
    
    // update of the switch/case state machine counter (1/10s)
    // it is used in 'loop()' to decide what actions to do (kind of scheduler)
    if (++Timer.tmp_state >= 10)      //this is true every 100ms, the variable Timer.tmp_state is always incremented
      {
      Timer.tmp_state = 0 ;
      // update 1/10s state machine counter (switch/case)
      if (++Timer.state >= 10)
        {
        Timer.stateFlags = 0xFFFF ;  // if bit is set to 1 scheduler can run 
        Timer.state = 0 ;
        }
      } 
      
    // update clock at 1 second rate
    if (++Timer.centisecs >= 100)    // this is true every 1000ms, the variable Timer.tmp_centisecs is incremented every 10ms (1 centisec)
      {
      Timer.centisecs = 0 ;
      
      // READ CPS counter
      if (++Counts.head >= MAX_COUNT_HISTORY)
        {
        Counts.head = 0 ;
        Counts.BufferFull = 1 ;  // TRUE
        }

        Counts.CPS[Counts.head] = timer1.getCount() ;  // read count from timer1
        timer1.setCount(0); // Reset counter to 0
      // check if count is more than 65536/s (overflow)
      if (Counts.BufferOverflow)
        {
        // fast "add" to 3rd significant byte!!!
        Counts.CPS[Counts.head] |= ((unsigned long)Counts.BufferOverflow << 16) ;
        Counts.BufferOverflow = 0 ;  // reset overflow counter
        }
      Counts.TotalSeconds++ ;
      
      // update minutes/hours/days    
      if (++Timer.secs >= 60)
        {
        Timer.secs = 0 ;
        if (++Timer.mins >=60)
          {
          Timer.mins = 0 ;
          if (++Timer.hours >= 24)
            {
            Timer.hours = 0 ;
            ++Timer.days ;
            }
          }
        }
      }
    }
}







