﻿using System;
using Unity.Burst;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Dots {
    [BurstCompile]
    public struct IntPair : System.IEquatable<IntPair> {
        public static IntPair zero = new IntPair(0, 0);
        public int x, y;
        public byte d;

        public IntPair(int x, int y) {
            d = 0;
            this.x = x;
            this.y = y;
        }

        public IntPair(int x) : this(x, x) { }

        public IntPair ascending {
            get { return x > y ? new IntPair(y, x) : this; }
        }

        public IntPair descending {
            get { return x < y ? new IntPair(y, x) : this; }
        }

        public bool Contains(int i) {
            return (x == i || y == i);
        }

        public static bool operator !=(IntPair e1, IntPair e2) {
            return !(e1.x == e2.x && e1.y == e2.y);
        }

        public static bool operator ==(IntPair e1, IntPair e2) {
            return (e1.x == e2.x && e1.y == e2.y);
        }

        public bool Equals(IntPair e) {
            return (e.x == x && e.y == y);
        }

        public override bool Equals(object obj) {
            IntPair e = (IntPair) obj;
            return (e.x == x && e.y == y);
        }

        public override int GetHashCode() {
            unchecked // Overflow is fine, just wrap
            {
                return x * 100000 + y;
            }
        }

        public override string ToString() {
            return string.Format("({0},{1})", x, y);
        }

        public static IntPair operator +(IntPair l, int r) {
            return new IntPair(l.x + r, l.y + r);
        }
        public static IntPair operator -(IntPair l, int r) {
            return new IntPair(l.x - r, l.y - r);
        }
        public static IntPair operator *(IntPair l, int r) {
            return new IntPair(l.x * r, l.y * r);
        }
        public static IntPair operator /(IntPair l, int r) {
            return new IntPair(l.x / r, l.y / r);
        }

        public static IntPair operator +(int l, IntPair r) {
            return new IntPair(r.x + l, r.y + l);
        }
        public static IntPair operator -(int l, IntPair r) {
            return new IntPair(r.x - l, r.y - l);
        }
        public static IntPair operator *(int l, IntPair r) {
            return new IntPair(r.x * l, r.y * l);
        }
        public static IntPair operator /(int l, IntPair r) {
            return new IntPair(r.x / l, r.y / l);
        }

        public static IntPair operator +(IntPair l, IntPair r) {
            return new IntPair(l.x + r.x, l.y + r.y);
        }
        public static IntPair operator -(IntPair l, IntPair r) {
            return new IntPair(l.x - r.x, l.y - r.y);
        }
        public static IntPair operator *(IntPair l, IntPair r) {
            return new IntPair(l.x * r.x, l.y * r.y);
        }
        public static IntPair operator /(IntPair l, IntPair r) {
            return new IntPair(l.x / r.x, l.y / r.y);
        }

        public static IntPair operator +(IntPair l, int2 r) {
            return new IntPair(l.x + r.x, l.y + r.y);
        }
        public static IntPair operator -(IntPair l, int2 r) {
            return new IntPair(l.x - r.x, l.y - r.y);
        }
        public static IntPair operator *(IntPair l, int2 r) {
            return new IntPair(l.x * r.x, l.y * r.y);
        }
        public static IntPair operator /(IntPair l, int2 r) {
            return new IntPair(l.x / r.x, l.y / r.y);
        }

        public static IntPair operator +(int2 l, IntPair r) {
            return new IntPair(l.x + r.x, l.y + r.y);
        }
        public static IntPair operator -(int2 l, IntPair r) {
            return new IntPair(l.x - r.x, l.y - r.y);
        }
        public static IntPair operator *(int2 l, IntPair r) {
            return new IntPair(l.x * r.x, l.y * r.y);
        }
        public static IntPair operator /(int2 l, IntPair r) {
            return new IntPair(l.x / r.x, l.y / r.y);
        }

        public static IntPair operator +(IntPair l, UIntPair r) {
            return new IntPair(l.x + r.x, l.y + r.y);
        }
        public static IntPair operator -(IntPair l, UIntPair r) {
            return new IntPair(l.x - r.x, l.y - r.y);
        }
        public static IntPair operator *(IntPair l, UIntPair r) {
            return new IntPair(l.x * r.x, l.y * r.y);
        }
        public static IntPair operator /(IntPair l, UIntPair r) {
            return new IntPair(l.x / r.x, l.y / r.y);
        }

        public static IntPair operator +(UIntPair l, IntPair r) {
            return new IntPair(l.x + r.x, l.y + r.y);
        }
        public static IntPair operator -(UIntPair l, IntPair r) {
            return new IntPair(l.x - r.x, l.y - r.y);
        }
        public static IntPair operator *(UIntPair l, IntPair r) {
            return new IntPair(l.x * r.x, l.y * r.y);
        }
        public static IntPair operator /(UIntPair l, IntPair r) {
            return new IntPair(l.x / r.x, l.y / r.y);
        }

        public static float2 operator *(IntPair l, float2 r) {
            return float2(l.x * r.x, l.y * r.y);
        }
        public static float2 operator *(float2 l, IntPair r) {
            return float2(l.x * r.x, l.y * r.y);
        }

        public static float3 operator *(IntPair l, float3 r) {
            return float3(l.x * r.x, l.y * r.y, r.z);
        }
        public static float3 operator *(float3 l, IntPair r) {
            return float3(l.x * r.x, l.y * r.y, l.z);
        }

        public static float2 operator *(IntPair l, float r) {
            return float2(l.x * r, l.y * r);
        }
        public static float2 operator /(IntPair l, float r) {
            return float2(l.x / r, l.y / r);
        }

        public static float2 operator *(float l, IntPair r) {
            return float2(l * r.x, l * r.y);
        }
        public static float2 operator /(float l, IntPair r) {
            return float2(l / r.x, l / r.y);
        }

        public static implicit operator UIntPair(IntPair pair) {
            return new UIntPair(pair.x, pair.y);
        }
        public static implicit operator int2(IntPair pair) {
            return int2(pair.x, pair.y);
        }
        public static implicit operator IntPair(int2 i) {
            return int2(i.x, i.y);
        }
    }
}
