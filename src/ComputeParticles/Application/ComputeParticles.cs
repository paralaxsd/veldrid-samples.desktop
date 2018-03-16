﻿using SampleBase;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;

namespace ComputeParticles
{
    public class ComputeParticles : SampleApplication
    {
        public const int ParticleCount = 1024;

        private DeviceBuffer _particleBuffer;
        private DeviceBuffer _screenSizeBuffer;
        private Shader _computeShader;
        private Pipeline _computePipeline;
        private Shader _vertexShader;
        private Shader _fragmentShader;
        private Pipeline _graphicsPipeline;
        private ResourceSet _graphicsParticleResourceSet;
        private CommandList _cl;
        private ResourceSet _screenSizeResourceSet;
        private ResourceSet _computeScreenSizeResourceSet;
        private ResourceSet _computeResourceSet;
        private bool _initialized;

        public ComputeParticles(ApplicationWindow window) : base(window)
        {
        }

        protected override void CreateResources(ResourceFactory factory)
        {
            _particleBuffer = factory.CreateBuffer(
                new BufferDescription(
                    (uint)Unsafe.SizeOf<ParticleInfo>() * ParticleCount,
                    BufferUsage.StructuredBufferReadWrite,
                    (uint)Unsafe.SizeOf<ParticleInfo>()));

            _screenSizeBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));

            _computeShader = factory.CreateShader(new ShaderDescription(
                ShaderStages.Compute,
                ReadEmbeddedAssetBytes($"Compute.{GetExtension(factory.BackendType)}"),
                "CS"));

            ResourceLayout particleStorageLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ParticlesBuffer", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)));

            ResourceLayout screenSizeLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ScreenSizeBuffer", ResourceKind.UniformBuffer, ShaderStages.Compute)));

            ComputePipelineDescription computePipelineDesc = new ComputePipelineDescription(
                _computeShader,
                new[] { particleStorageLayout, screenSizeLayout },
                1, 1, 1);
            _computePipeline = factory.CreateComputePipeline(ref computePipelineDesc);

            _computeResourceSet = factory.CreateResourceSet(new ResourceSetDescription(particleStorageLayout, _particleBuffer));

            _computeScreenSizeResourceSet = factory.CreateResourceSet(new ResourceSetDescription(screenSizeLayout, _screenSizeBuffer));

            _vertexShader = factory.CreateShader(new ShaderDescription(
                ShaderStages.Vertex,
                ReadEmbeddedAssetBytes($"Vertex.{GetExtension(factory.BackendType)}"),
                "VS"));
            _fragmentShader = factory.CreateShader(new ShaderDescription(
                ShaderStages.Fragment,
                ReadEmbeddedAssetBytes($"Fragment.{GetExtension(factory.BackendType)}"),
                "FS"));

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                Array.Empty<VertexLayoutDescription>(),
                new[]
                {
                    _vertexShader,
                    _fragmentShader
                });

            particleStorageLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ParticlesBuffer", ResourceKind.StructuredBufferReadOnly, ShaderStages.Vertex)));

            screenSizeLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
                new ResourceLayoutElementDescription("ScreenSizeBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

            GraphicsPipelineDescription particleDrawPipelineDesc = new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.Default,
                PrimitiveTopology.PointList,
                shaderSet,
                new[] { particleStorageLayout, screenSizeLayout },
                MainSwapchain.Framebuffer.OutputDescription);

            _graphicsPipeline = factory.CreateGraphicsPipeline(ref particleDrawPipelineDesc);

            _graphicsParticleResourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                particleStorageLayout,
                _particleBuffer));

            _screenSizeResourceSet = factory.CreateResourceSet(new ResourceSetDescription(
                screenSizeLayout,
                _screenSizeBuffer));

            _cl = factory.CreateCommandList();

            InitResources(factory);
            _initialized = true;
        }

        private string GetExtension(GraphicsBackend backendType)
        {
            return backendType == GraphicsBackend.Direct3D11 ? "hlsl.bytes"
                : backendType == GraphicsBackend.Vulkan ? "spv"
                    : backendType == GraphicsBackend.Metal ? "metal"
                        : backendType == GraphicsBackend.OpenGL ? "430.glsl"
                            : "300.glsles";
        }

        private void InitResources(ResourceFactory factory)
        {
            _cl.Begin();
            _cl.UpdateBuffer(_screenSizeBuffer, 0, new Vector4(Window.Width, Window.Height, 0, 0));

            ParticleInfo[] initialParticles = new ParticleInfo[ParticleCount];
            Random r = new Random();
            for (int i = 0; i < ParticleCount; i++)
            {
                ParticleInfo pi = new ParticleInfo(
                    new Vector2((float)(r.NextDouble() * Window.Width), (float)(r.NextDouble() * Window.Height)),
                    new Vector2((float)(r.NextDouble() * 3), (float)(r.NextDouble() * 3)),
                    new Vector4(0.4f + (float)r.NextDouble() * .6f, 0.4f + (float)r.NextDouble() * .6f, 0.4f + (float)r.NextDouble() * .6f, 1));
                initialParticles[i] = pi;
            }
            _cl.UpdateBuffer(_particleBuffer, 0, initialParticles);

            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.WaitForIdle();
        }

        protected override void HandleWindowResize()
        {
            GraphicsDevice.UpdateBuffer(_screenSizeBuffer, 0, new Vector4(Window.Width, Window.Height, 0, 0));
        }

        protected override void Draw(float deltaSeconds)
        {
            if (!_initialized) { return; }

            _cl.Begin();

            _cl.SetPipeline(_computePipeline);
            _cl.SetComputeResourceSet(0, _computeResourceSet);
            _cl.SetComputeResourceSet(1, _computeScreenSizeResourceSet);
            _cl.Dispatch(1024, 1, 1);

            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.SetFullViewports();
            _cl.SetFullScissorRects();
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.SetPipeline(_graphicsPipeline);
            _cl.SetGraphicsResourceSet(0, _graphicsParticleResourceSet);
            _cl.SetGraphicsResourceSet(1, _screenSizeResourceSet);
            _cl.Draw(ParticleCount, 1, 0, 0);
            _cl.End();

            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
        }
    }

    struct ParticleInfo
    {
        // TODO: REVERT ALL OF THIS WHEN MONO IS FIXED.

        //public Vector2 Position;
        public float PositionX;
        public float PositionY;

        //public Vector2 Velocity;
        public float VelocityX;
        public float VelocityY;

        //public Vector4 Color;
        public float ColorX;
        public float ColorY;
        public float ColorZ;
        public float ColorW;

        public ParticleInfo(Vector2 position, Vector2 velocity, Vector4 color)
        {
            PositionX = position.X;
            PositionY = position.Y;

            VelocityX = velocity.X;
            VelocityY = velocity.Y;

            ColorX = color.X;
            ColorY = color.Y;
            ColorZ = color.Z;
            ColorW = color.W;
        }
    }
}