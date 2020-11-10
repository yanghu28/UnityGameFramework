﻿//-----------------------------------------------------------------------
// <copyright>
//     Copyright (c) 2018 wanderer. All rights reserved.
// </copyright>
// <describe> #ui管理类# </describe>
// <email> dutifulwanderer@gmail.com </email>
// <time> #2018年6月22日 17点01分# </time>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UniRx;
using Cysharp.Threading.Tasks;

namespace Wanderer.GameFramework
{
    public sealed class UIManager : GameFrameworkModule
    {
        //事件管理器
        private EventManager _event;
        private UIEnterEventArgs _uiEnterArgs;
        private UIExitEventArgs _uiExitArgs;
        private UIPauseEventArgs _uiPauseArgs;
        private UIResumeEventArgs _uiResumeArgs;
        private UITween _uiTweener;

        //资源管理器
        private ResourceManager _resource;
        //ui 列表
        private List<IUIContext> _activeUIContextList = new List<IUIContext>();
        //所有的uiview
        private readonly Dictionary<IUIContext, UIView> _allUIView = new Dictionary<IUIContext, UIView>();
        
        //uicontext管理器
        public UIContextManager UIContextMgr { get; private set; }

        #region 构造函数
        public UIManager()
        {
            _uiTweener = new UITween();
            UIContextMgr = new UIContextManager();
            //获取资源模块
            _resource = GameFrameworkMode.GetModule<ResourceManager>();
            //获取事件模块
            _event = GameFrameworkMode.GetModule<EventManager>();
            //ui事件
            _uiEnterArgs = new UIEnterEventArgs();
            _uiExitArgs = new UIExitEventArgs();
            _uiPauseArgs = new UIPauseEventArgs();
            _uiResumeArgs = new UIResumeEventArgs();
        }
        #endregion

        #region 外部接口
      
        /// <summary>
        /// 推送UI
        /// </summary>
        /// <param name="uiContext"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public IUITween Push(IUIContext uiContext, params object[] parameters)
        {
            _uiTweener.Flush();
            if (uiContext == null)
            {
                return _uiTweener;
            }

            //处理之前的ui
            if (_activeUIContextList.Count > 0)
            {
                //如果界面已经打开 则不在执行
                for (int i = 0; i < _activeUIContextList.Count; i++)
                {
                    if (uiContext == _activeUIContextList[i])
                        return _uiTweener;
                }

                //获取最上层的ui
                IUIContext lastUIContext = _activeUIContextList[_activeUIContextList.Count - 1];
                UIView uiView = GetUIView(lastUIContext);
                //设置之前的uiview
                _uiTweener.SetLastUIView(uiView);

                //触发暂停事件
                _uiPauseArgs.UIView = uiView;
                _event.Trigger(this, _uiPauseArgs);
                //响应暂停接口
                uiView.OnPause(lastUIContext);
            }
            //处理新的ui
            _activeUIContextList.Add(uiContext);
            UIView newUiView = GetUIView(uiContext);
            newUiView.OnEnter(uiContext, parameters);
            //触发打开事件
            _uiEnterArgs.UIView = newUiView;
            _event.Trigger(this, _uiEnterArgs);
            //设置打开的uiview
            _uiTweener.SetNextUIView(newUiView);

            return _uiTweener;
        }

        /// <summary>
        /// 推送UI
        /// </summary>
        /// <param name="assetPath"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public IUITween Push(string assetPath, params object[] parameters)
        {
            IUIContext uiContext = UIContextMgr[assetPath];
            if (uiContext != null)
            {
                IUITween uiTween = Push(uiContext);
                return uiTween;
            }
            return null;
        }

        /// <summary>
        /// 弹出UI
        /// </summary>
        /// <param name="setHide"></param>
        /// <returns></returns>
        public IUITween Pop(bool setHide = true)
        {
            _uiTweener.Flush();
            //移除当前UI
            if (_activeUIContextList.Count > 0)
            {
                //获取最上层的ui
                IUIContext lastUIContext = _activeUIContextList[_activeUIContextList.Count - 1];
                _uiTweener = (UITween)Close(lastUIContext, setHide);
            }
            return _uiTweener;
        }
       
        /// <summary>
        /// 关闭特定的UI
        /// </summary>
        /// <param name="uiContext"></param>
        /// <param name="setHide"></param>
        /// <returns></returns>
        public IUITween Close(IUIContext uiContext, bool setHide = true)
        {
            _uiTweener.Flush();

            bool needResume = false;
            if (_activeUIContextList.Count > 0 && _activeUIContextList.Contains(uiContext))
            {
                if (_activeUIContextList[_activeUIContextList.Count - 1] == uiContext)
                {
                    needResume = true;
                }
                _activeUIContextList.Remove(uiContext);
                //回收重复的UIContext
                if (uiContext.Multiple)
                {
                    UIContextMgr.ReleaseUIContext(uiContext);
                }
                //获取uiview
                UIView lastUiView;
                if (_allUIView.TryGetValue(uiContext, out lastUiView))
                {
                    //触发关闭事件
                    _uiExitArgs.UIView = lastUiView;
                    _event.Trigger(this, _uiExitArgs);

                    lastUiView.OnExit(uiContext);
                    //设置弹出的UI
                    _uiTweener.SetLastUIView(lastUiView);
                }
            }
            //触发恢复界面
            if (needResume && _activeUIContextList.Count > 0)
            {
                //获取最上层的ui
                IUIContext lastUIContext = _activeUIContextList[_activeUIContextList.Count - 1];
                UIView lastUiView;
                if (_allUIView.TryGetValue(lastUIContext, out lastUiView))
                {
                    lastUiView.OnResume(lastUIContext);
                    //触发恢复事件
                    _uiResumeArgs.UIView = lastUiView;
                    _event.Trigger(this, _uiResumeArgs);
                    //设置最上层的UI
                    _uiTweener.SetNextUIView(lastUiView);
                }
            }

            //默认设置隐藏
            if (setHide)
            {
                if (_uiTweener.LastUIView != null)
                {
                    UIView uiView = _uiTweener.LastUIView as UIView;
                    uiView.gameObject.SetActive(false);
                }
            }

            return _uiTweener;
        }

        /// <summary>
        /// 关闭特定的UI
        /// </summary>
        /// <param name="uiView"></param>
        /// <param name="setHide"></param>
        /// <returns></returns>
        public IUITween Close(UIView uiView, bool setHide = true)
        {
			foreach (var item in _allUIView)
			{
                if (item.Value == uiView)
                {
                    IUITween uiTween = Close(item.Key, setHide);
                    return uiTween;
                }
			}
            return null;
        }

        /// <summary>
        /// 获取最顶上的UIView
        /// </summary>
        /// <returns></returns>
        public UIView Peek()
        {
            if (_activeUIContextList.Count > 0)
            {
                return GetUIView(_activeUIContextList[_activeUIContextList.Count-1]);
            }
            return null;
        }

        /// <summary>
        /// 获取UIView
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public UIView GetActiveUIView(string assetPath)
        {
			for (int i = 0; i < _activeUIContextList.Count; i++)
			{
                if (assetPath.Equals(_activeUIContextList[i].AssetPath))
                {
                    return GetUIView(_activeUIContextList[i]);
                }
			}
            return null;
        }

        #endregion

        #region 内部函数
        //获取ui界面
        private UIView GetUIView(IUIContext uiContext)
        {
            UIView uiView;
            if (!_allUIView.TryGetValue(uiContext, out uiView)|| uiView==null)
            {
                GameObject uiViewSource = _resource.LoadAssetSync<GameObject>(uiContext.AssetPath);
                if (uiViewSource == null)
                    throw new GameException("uiview path not found:" + uiContext.AssetPath);

                GameObject uiViewClone = GameObject.Instantiate(uiViewSource);
                uiView = uiViewClone.GetComponent<UIView>();
                if (uiView == null)
                    return null;
                _allUIView[uiContext] = uiView;
                return uiView;
            }
            uiView.gameObject.SetActive(true);
            return uiView;
        }
        #endregion


        #region 重写函数
        public override void OnClose()
        {
            _activeUIContextList.Clear();

            foreach (var item in _allUIView.Values)
            {
                MonoBehaviour.Destroy(item);
            }
            _allUIView.Clear();
        }
        #endregion
    }
}
