using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Camera orbită (tema): privește mereu spre o țintă (Target) și se mișcă pe o sferă
/// de rază Distance în jurul ei, poziția fiind determinată de Azimuth și Elevation.
/// </summary>
public class OrbitCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------

    /// <summary>Punctul spre care privește camera.</summary>
    public Vector3 Target { get; set; }

    /// <summary>Raza sferei pe care se află camera față de Target.</summary>
    public float Distance { get; private set; }

    /// <summary>Azimutul, în grade: unghiul în jurul axei Y (similar cu yaw).</summary>
    public float Azimuth { get; private set; }

    /// <summary>Elevația, în grade: unghiul deasupra planului orizontal (similar cu pitch).</summary>
    public float Elevation { get; private set; }

    // --- Parametri ----------------------------------------------------------------

    public const float MaxElevation = 89.0f;
    public const float MinDistance = 1.5f;
    public const float MaxDistance = 40.0f;

    public float MouseSensitivity = 0.25f;      // grade pe pixel la drag cu clic stânga
    public float KeyRotateSpeed = 75.0f;        // grade pe secundă cu săgețile
    public float ZoomStep = 0.8f;               // unități de distanță pe pas de rotiță
    public float TargetMoveSpeed = 4.0f;        // viteza deplasării țintei cu W A S D (bonus)

    private static readonly Vector3 WorldUp = Vector3.UnitY;
    private static readonly Vector3 StartTarget = new(0.0f, 1.0f, -1.5f);
    private const float StartDistance = 8.0f;
    private const float StartAzimuth = 90.0f;
    private const float StartElevation = 20.0f;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "Orbită";

    /// <summary>
    /// Poziția pe sferă: țintă + distanță * (cos el * cos az, sin el, cos el * sin az).
    /// </summary>
    public override Vector3 Position
    {
        get
        {
            float azRad = MathHelper.DegreesToRadians(Azimuth);
            float elRad = MathHelper.DegreesToRadians(Elevation);

            Vector3 offset = new(
                MathF.Cos(elRad) * MathF.Cos(azRad),
                MathF.Sin(elRad),
                MathF.Cos(elRad) * MathF.Sin(azRad));

            return Target + Distance * offset;
        }
    }

    /// <summary>Direcția unitate de la cameră spre țintă.</summary>
    public override Vector3 Front => Vector3.Normalize(Target - Position);

    /// <summary>Matricea view: camera privește mereu spre Target.</summary>
    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(Position, Target, WorldUp);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // --- 1. Rotația cu mouse-ul (clic stânga ținut apăsat) ---------------------
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // --- 2. Rotația de la tastatură (săgețile) ---------------------------------
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth += KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth -= KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= KeyRotateSpeed * dt;

        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);

        // --- 3. Zoom cu rotița mouse-ului (modifică Distance, nu FOV) --------------
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            Distance = Math.Clamp(Distance - mouse.ScrollDelta.Y * ZoomStep, MinDistance, MaxDistance);
        }

        // --- 4. Bonus: W A S D mută ținta în planul orizontal ----------------------
        Vector3 front = Front;
        Vector3 forward = new(front.X, 0.0f, front.Z);
        if (forward.LengthSquared > 0.0f)
        {
            forward = Vector3.Normalize(forward);
        }
        Vector3 right = Vector3.Normalize(Vector3.Cross(forward, WorldUp));

        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= forward;
        if (keyboard.IsKeyDown(Keys.D)) move += right;
        if (keyboard.IsKeyDown(Keys.A)) move -= right;

        if (move.LengthSquared > 0.0f)
        {
            Target += Vector3.Normalize(move) * TargetMoveSpeed * dt;
        }
    }

    /// <summary>
    /// Bonus: așază camera orbită astfel încât să preia exact punctul de vedere al camerei FPS,
    /// cu ținta la distanța <paramref name="distance"/> în fața acesteia.
    /// </summary>
    public void SyncFromFps(Vector3 fpsPosition, Vector3 fpsFront, float distance = 5.0f)
    {
        Distance = Math.Clamp(distance, MinDistance, MaxDistance);
        Target = fpsPosition + fpsFront * Distance;

        // Vectorul unitar țintă -> cameră: o = -front
        Vector3 o = Vector3.Normalize(-fpsFront);
        float elRad = MathF.Asin(Math.Clamp(o.Y, -1.0f, 1.0f));
        float azRad = MathF.Atan2(o.Z, o.X);

        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(elRad), -MaxElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(azRad);
    }

    public override void Reset()
    {
        Target = StartTarget;
        Distance = StartDistance;
        Azimuth = StartAzimuth;
        Elevation = StartElevation;
        FieldOfView = 60.0f;
    }

    public override IEnumerable<string> Describe()
    {
        Vector3 pos = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {NormalizeAngle(Azimuth),7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({pos.X,6:F2}, {pos.Y,6:F2}, {pos.Z,6:F2})";
    }

    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}