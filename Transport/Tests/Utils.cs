using System;
using System.Collections;
using System.Threading;

namespace Cube.Transport.Tests {
    
    public class Utils {

        public static IEnumerator RunTill(Func<bool> test, float seconds) {
            yield return RunTill(test, seconds, 60);
        }

        public static IEnumerator RunTill(Func<bool> test, float seconds, int fps) {
            float timeLeft = seconds;
            float frameTime = seconds / (float)fps;
            
            while (timeLeft > 0f && !test()) {
                Thread.Sleep((int)(1000f * frameTime));
                yield return null;
                timeLeft -= frameTime;
            }
        }
                
    }
    
}
