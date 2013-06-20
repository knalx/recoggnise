using System;

public class Mask
{   
    //matrix x,y. value = zone number.
    public short[,] Matrix;
    // index - zone number, value - summ of this zone.
    public int[] ZoneSum;
    public int[] PrimeZoneSum;
    public int[] DiffZoneSum;
    // probability of this mask
    public int Chance;


    public int Haight;
    public int Width;
    public bool Visible = false;

    public int xStart = 160;
    public int yStart = 75;

    public Mask(int Haight = 480, int Width = 640, int zs = 6)
    {
        this.Haight = Haight;
        this.Width = Width;

        ZoneSum = new int[zs];
        PrimeZoneSum = new int[zs];
        Matrix = new short[Haight, Width];
        for (int i = 0; i < Haight; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                Matrix[i, j] = 0;
            }
        }
    }
   
    public void resetMask (int Haight = 480, int Width = 640 ){
          Matrix = new short [Haight,Width];
          for (int i = 0; i < Haight; i++)
          {
                for (int j = 0; j < Width; j++)
                {
                    Matrix[i, j] = 0;
                }
           }
     }

    public void drawSquare( int mHeight, int mWidth, int hStart = 0, int wStart = 0, short zoneNumber = 0)
    {
        for (int i = hStart; i < hStart + mHeight; i++)
        {
            for (int j = wStart; j < wStart + mWidth; j++)
            {
              
                Matrix[i, j] = zoneNumber;
            }
        }
    }

    

    public void calcSum(int [,] inp, int hStart, int wStart)
    {
        for ( int i = 0 ; i <ZoneSum.Length; i++){
        ZoneSum[i] = 0;
        }

        for (int i = 0; i < Haight; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                ZoneSum[Matrix[i, j]] += inp[i + hStart,j + wStart];
            }
        }


    }

    public void calcPrimeSum(int[,] inp)
    {
        for (int i = 0; i < ZoneSum.Length; i++)
        {
            PrimeZoneSum[i] = 0;
        }

        for (int i = 0; i < Haight; i++)
        {
            for (int j = 0; j < Width; j++)
            {
                PrimeZoneSum[Matrix[i, j]] += inp[i + xStart, j + yStart];
            }
        }
    
    }

    public int findDiff()
    {
        int diff = 0;
        DiffZoneSum = new int[PrimeZoneSum.Length];
        for (int i = 1; i < DiffZoneSum.Length; i++)
        {
            DiffZoneSum[i] = Math.Abs(PrimeZoneSum[i] - ZoneSum[i]);
            diff += DiffZoneSum[i];
        }

       

        return diff;
    }




	
}
